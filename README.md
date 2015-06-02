# findmagic

findmagic is a free (as in speech AND beer, how cool is that?) tool to find libraries in statically linked binaries. All you need is a binary that contains the target library that includes symbols to generate definitions for you. The reference binary should include the library in about the same version (not neccessarily the exact version that was used for linking) and needs to be statically linked. Protip: just use a dummy that uses just enough for a minimal example of the library, then strip the main() function from the list of those to be analyzed.

Also, you need a recent-ish mono, aswell as the [capstone library](http://www.capstone-engine.org/) and [corresponding c# bindings](https://github.com/9ee1/Capstone.NET). For Mono, use [dllmap](http://www.mono-project.com/docs/advanced/pinvoke/). However, it is currently untested wether it works on unix (depends on the capstone c# bindings).

## Usage

**Step 1:** Analyze a binary that was linked against the target library. Must contain symbols. For analyzation, you need a json list of all functions:
```json
[
{ "Name" : "this_is_my_awesome_library_function", "StartAddress" : 12341234, "EndAddress": 12342345 },
]
```

Note that json doesn't handle hexadecimal view quite well.

**Step 2:** Apply the definition file to a binary.
**Step 3:** Profit.

For command line reference, use --help.
```
$ ./findmagic.exe --help
findmagic 1.0.0.0
Copyright ©  2015

  -m, --mode             Required. (Default: match) Mode - must match
                         ^(match|analyze)$. Default: match

  -e, --elf              Required. Input ELF to be analyzed

  -s, --subs             Required. Subroutine definition file

  -c, --constants        Constants definition file

  -o, --fileout          (Default: ) Output file where reports will be saved to

  -d, --data-sections    (Default: ^\.(ro)?data$) Regex matching the names of
                         allowed sections for string xref resolution, standard
                         ^\.(ro)?data$ if omitted

  --help                 Display this help screen.
```

# License
GPLv3 or later. See the LICENSE file for details.
![GPLv3](http://www.gnu.org/graphics/gplv3-127x51.png)