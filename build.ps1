$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

# detect buildx, ErrorActionPreference will ensure the script stops execution if not found
docker buildx version

# define temporary directory
$tempdirectory = "$pwd\temp"
# define services to patch
$components = "Api","Identity"

# delete old directories / files if applicable
if (Test-Path "$tempdirectory" -PathType Container) {
	Remove-Item "$tempdirectory" -Recurse -Force
}

if (Test-Path -Path "$pwd\src\licenseGen\Core.dll" -PathType Leaf) {
	Remove-Item "$pwd\src\licenseGen\Core.dll" -Force
}

if (Test-Path -Path "$pwd\src\licenseGen\cert.pfx" -PathType Leaf) {
	Remove-Item "$pwd\src\licenseGen\cert.pfx" -Force
}

if (Test-Path -Path "$pwd\src\bitBetter\cert.cer" -PathType Leaf) {
	Remove-Item "$pwd\src\bitBetter\cert.cer" -Force
}

if (Test-Path "$pwd\.keys\cert.cert" -PathType Leaf) {
	Rename-Item -Path "$pwd\.keys\cert.cert" -NewName "$pwd\.keys\cert.cer"
}

# generate keys if none are available
if (!(Test-Path "$pwd\.keys" -PathType Container)) {
	.\generateKeys.ps1
}

# copy the key to bitBetter
Copy-Item "$pwd\.keys\cert.cer" -Destination "$pwd\src\bitBetter"

# build bitBetter and clean the source directory after
docker build --no-cache -t bitbetter/bitbetter "$pwd\src\bitBetter"
Remove-Item "$pwd\src\bitBetter\cert.cer" -Force

# gather all running instances, cannot run a wildcard filter on Ancestor= :(, does find all where name = *bitwarden*
$oldinstances = docker container ps --all -f Name=bitwarden --format '{{.ID}}'

# stop and remove all running instances
foreach ($instance in $oldinstances) {
	docker stop $instance
	docker rm $instance
}

# update bitwarden itself
if ($args[0] -eq 'update') {
	docker pull ghcr.io/bitwarden/self-host:beta
} else {
	$confirmation = Read-Host "Update (or get) bitwarden source container (y/n)"
	if ($confirmation -eq 'y') {
		docker pull ghcr.io/bitwarden/self-host:beta
	}
}

# stop and remove previous existing patch(ed) container
$oldinstances = docker container ps --all -f Ancestor=bitwarden-patched --format '{{.ID}}'
foreach ($instance in $oldinstances) {
	docker stop $instance
	docker rm $instance
}
$oldinstances = docker image ls bitwarden-patched --format '{{.ID}}'
foreach ($instance in $oldinstances) {
	docker image rm $instance
}

# remove old extract containers
$oldinstances = docker container ps --all -f Name=bitwarden-extract --format '{{.ID}}'
foreach ($instance in $oldinstances) {
	docker stop $instance
	docker rm $instance
}

# start a new bitwarden instance so we can patch it
$patchinstance = docker run -d --name bitwarden-extract ghcr.io/bitwarden/self-host:beta

# create our temporary directory
New-item -ItemType Directory -Path $tempdirectory

# extract the files that need to be patched from the services that need to be patched into our temporary directory
foreach ($component in $components) {
	New-item -itemtype Directory -path "$tempdirectory\$component"
	docker cp $patchinstance`:/app/$component/Core.dll "$tempdirectory\$component\Core.dll"
}

# stop and remove our temporary container
docker stop bitwarden-extract
docker rm bitwarden-extract

# run bitBetter, this applies our patches to the required files
docker run -v "$tempdirectory`:/app/mount" --rm bitbetter/bitbetter

# create a new image with the patched files
docker build . --tag bitwarden-patched --file "$pwd\src\bitBetter\Dockerfile-bitwarden-patch"

# start all user requested instances
if (Test-Path -Path "$pwd\.servers\serverlist.txt" -PathType Leaf) {
	foreach($line in Get-Content "$pwd\.servers\serverlist.txt") {
		if (!($line.StartsWith("#"))) {
			Invoke-Expression "& $line"
		}
	}
}

# remove our bitBetter image
docker image rm bitbetter/bitbetter

# copy our patched library to the licenseGen source directory
Copy-Item "$tempdirectory\Identity\Core.dll" -Destination "$pwd\src\licenseGen"
Copy-Item "$pwd\.keys\cert.pfx" -Destination "$pwd\src\licenseGen"

# build the licenseGen
docker build -t bitbetter/licensegen "$pwd\src\licenseGen"

# clean the licenseGen source directory
Remove-Item "$pwd\src\licenseGen\Core.dll" -Force
Remove-Item "$pwd\src\licenseGen\cert.pfx" -Force

# remove our temporary directory
Remove-Item "$tempdirectory" -Recurse -Force