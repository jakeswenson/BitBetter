$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($($args.Count) -lt 1) {
    echo "USAGE: <License Gen action> [License Gen args...]"
    echo "ACTIONS:"
    echo " interactive"
    echo " user"
    echo " org"
	Exit 1
}

if ($args[0] -eq "interactive") {
    docker run -it --rm bitbetter/licensegen interactive
} else {
    docker run bitbetter/licensegen $args
}
