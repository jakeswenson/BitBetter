# define temporary directory
$tempdirectory = "$pwd\temp"
# define services to patch
$components = "Api","Identity"

# delete old directories / files if applicable
if (Test-Path "$tempdirectory") {
	Remove-Item "$tempdirectory" -Recurse -Force
}

if (Test-Path -Path "$pwd\licenseGen\Core.dll" -PathType Leaf) {
	Remove-Item "$pwd\licenseGen\Core.dll" -Force
}

if (Test-Path -Path "$pwd\licenseGen\cert.pfx" -PathType Leaf) {
	Remove-Item "$pwd\licenseGen\cert.pfx" -Force
}

if (Test-Path -Path "$pwd\bitBetter\cert.cert" -PathType Leaf) {
	Remove-Item "$pwd\bitBetter\cert.cert" -Force
}

# generate keys if none are available
if (!(Test-Path "$pwd\.keys")) {
	.\generateKeys.ps1
}

# copy the key to bitBetter and licenseGen
Copy-Item "$pwd\.keys\cert.cert" -Destination "$pwd\bitBetter"
Copy-Item "$pwd\.keys\cert.pfx" -Destination "$pwd\licenseGen"

# build bitBetter and clean the source directory after
docker build -t bitbetter/bitbetter "$pwd\bitBetter"
Remove-Item "$pwd\bitBetter\cert.cert" -Force

# gather all running instances
$oldinstances = docker container ps --all -f Name=bitwarden --format '{{.ID}}'

# stop all running instances
foreach ($instance in $oldinstances) {
	docker stop $instance
	docker rm $instance
}

# update bitwarden itself
if ($args[0] -eq 'y')
{
	docker pull bitwarden/self-host:beta
}
else
{
	$confirmation = Read-Host "Update (or get) bitwarden source container"
	if ($confirmation -eq 'y') {
		docker pull bitwarden/self-host:beta
	}
}

# stop and remove previous existing patch(ed) container
docker stop bitwarden-patch
docker rm bitwarden-patch
docker image rm bitwarden-patch

# start a new bitwarden instance so we can patch it
$patchinstance = docker run -d --name bitwarden-patch bitwarden/self-host:beta

# create our temporary directory
New-item -ItemType Directory -Path $tempdirectory

# extract the files that need to be patched from the services that need to be patched into our temporary directory
foreach ($component in $components) {
	New-item -itemtype Directory -path "$tempdirectory\$component"
	docker cp $patchinstance`:/app/$component/Core.dll "$tempdirectory\$component\Core.dll"
}

# run bitBetter, this applies our patches to the required files
docker run -v "$tempdirectory`:/app/mount" --rm bitbetter/bitbetter

# copy the patched files back into the temporary instance
foreach ($component in $components) {
	docker cp "$tempdirectory\$component\Core.dll" $patchinstance`:/app/$component/Core.dll
}

# create a new image from our patched instanced
docker commit $patchinstance bitwarden-patch

# stop and remove our temporary container
docker stop bitwarden-patch
docker rm bitwarden-patch

# copy our patched library to the licenseGen source directory
Copy-Item "$tempdirectory\Identity\Core.dll" -Destination "$pwd\licenseGen"

# remove our temporary directory
Remove-Item "$tempdirectory" -Recurse -Force

# start all user requested instances
foreach($line in Get-Content "$pwd\.servers\serverlist.txt") {
	Invoke-Expression "& $line"
}

# remove our bitBetter image
docker image rm bitbetter/bitbetter

# build the licenseGen
docker build -t bitbetter/licensegen "$pwd\licenseGen"

# clean the licenseGen source directory
Remove-Item "$pwd\licenseGen\Core.dll" -Force
Remove-Item "$pwd\licenseGen\cert.pfx" -Force