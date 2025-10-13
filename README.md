# png-to-pi1

A simple CLI tool to convert PNG images to PI1 format for the Atari ST.


```shell
Usage:
  PngToPi1 [command] [options]

Options:
  -?, -h, --help  Show help and usage information
  --version       Show version information

Commands:
  convert <input> <output>  Converts a 320x200 indexed PNG file to an Atari ST PI1 file. The input file must have a 
                            16-color palette with an extra entry for transparency at the beginning.
```

Example:

```shell
$ PngToPi1 input.png output.pi1
```
