using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace findmagic
{
    class Options
    {
        [Option('m', "mode", DefaultValue = "match", HelpText = "Mode - must match ^(match|analyze)$. Default: match", Required=true)]
        public string Mode { get; set; }

        [Option('e', "elf", Required = true,
          HelpText = "Input ELF to be analyzed")]
        public string InputElfPath { get; set; }

        [Option('s', "subs", Required = true,
          HelpText = "Subroutine definition file")]
        public string SubroutineDefinitionsPath { get; set; }

        [Option('c', "constants", Required = false, HelpText = "Constants definition file")]
        public string ConstantsFile { get; set; }

        [Option('o', "fileout", Required = false, DefaultValue = "", HelpText = "Output file where reports will be saved to")]
        public string OutputFilePath { get; set; }

        [Option('d', "data-sections", Required = false, DefaultValue = @"^\.(ro)?data$", HelpText = @"Regex matching the names of allowed sections for string xref resolution, standard ^\.(ro)?data$ if omitted")]
        public string DataSectionPattern { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

        
    }
}
