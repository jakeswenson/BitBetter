if (Test-Path "$pwd\temp") {
	Remove-Item "$pwd\temp" -Recurse -Force
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

if (!(Test-Path "$pwd\.keys")) {
	.\generateKeys.ps1
}

Copy-Item "$pwd\.keys\cert.cert" -Destination "$pwd\bitBetter"
Copy-Item "$pwd\.keys\cert.pfx" -Destination "$pwd\licenseGen"
docker build -t bitbetter/bitbetter "$pwd\bitBetter"
Remove-Item "$pwd\bitBetter\cert.cert" -Force

$components = "Api","Identity"
$oldinstances = docker container ps --all -f Name=bitwarden --format '{{.ID}}'

foreach ($instance in $oldinstances) {
	docker stop $instance
	docker rm $instance
}

docker pull bitwarden/self-host:beta

docker stop bitwarden-patch
docker rm bitwarden-patch
docker image rm bitwarden-patch
$patchinstance = docker run -d --name bitwarden-patch bitwarden/self-host:beta

New-item -ItemType Directory -Path "$pwd\temp"
foreach ($component in $components) {
	New-item -itemtype Directory -path "$pwd\temp\$component"
	docker cp $patchinstance`:/app/$component/Core.dll "$pwd\temp\$component\Core.dll"
}

docker run -v "$pwd\temp:/app/mount" --rm bitbetter/bitbetter

foreach ($component in $components) {
	docker cp -a "$pwd\temp\$component\Core.dll" $patchinstance`:/app/$component/Core.dll
}

Copy-Item "$pwd\temp\Identity\Core.dll" -Destination "$pwd\licenseGen"
Remove-Item "$pwd\temp" -Recurse -Force

docker commit $patchinstance bitwarden-patch
docker stop bitwarden-patch
docker rm bitwarden-patch

$newinstances = @()
foreach($line in Get-Content "$pwd\.servers\serverlist.txt") {
	$newinstace = @(Invoke-Expression "& $line")
	$newinstances += $newinstace
}

foreach ($instance in $newinstances) {
	docker start $instance
}

docker image rm bitbetter/bitbetter

docker build -t bitbetter/licensegen "$pwd\licenseGen"
Remove-Item "$pwd\licenseGen\Core.dll" -Force
Remove-Item "$pwd\licenseGen\cert.pfx" -Force