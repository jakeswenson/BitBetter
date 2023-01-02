if ($($args.Count) -lt 1) {
    echo "USAGE: <License Gen action> [License Gen args...]"
    echo "ACTIONS:"
    echo " interactive"
    echo " user"
    echo " org"
	Exit 1
}

if ($args[0] = "interactive") {
	$shiftedarray = $args[1 .. ($args.count-1)]
    docker run -it --rm bitbetter/licensegen "$shiftedarray"
} else {
    docker run bitbetter/licensegen "$shiftedarray"
}
