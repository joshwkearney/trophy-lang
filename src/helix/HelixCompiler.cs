﻿using Helix.Parsing;
using Helix.Generation;
using Helix.Analysis.Flow;
using Helix.Analysis.TypeChecking;

namespace Helix {
    public class HelixCompiler {
        private readonly string header;
        private readonly string input;

        public HelixCompiler(string header, string input) {          
            this.header = header;
            this.input = input;
        }

        public string Compile() {
            // ToList() is after each step so lazy evaluation doesn't mess
            // up the order of the steps

            var input = this.input
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Replace("\t", "    ");

            var parser = new Parser(input);
            var types = new TypeFrame();
            var flow = new FlowFrame(types);
            var writer = new CWriter(this.header, types.TypeDeclarations);
            var parseStats = parser.Parse();

            foreach (var stat in parseStats) {
                stat.DeclareNames(types);
            }

            foreach (var stat in parseStats) {
                stat.DeclareTypes(types);
            }

            var stats = parseStats.Select(x => x.CheckTypes(types)).ToArray();

            foreach (var stat in stats) {
                stat.AnalyzeFlow(flow);
            }

            foreach (var stat in stats) {
                stat.GenerateCode(flow, writer);
            }

            return writer.ToString();            
        }
    }
}
