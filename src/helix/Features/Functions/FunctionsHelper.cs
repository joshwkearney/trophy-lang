﻿using Helix.Analysis;
using Helix.Analysis.Types;
using Helix.Features.Variables;
using Helix.Parsing;

namespace Helix.Features.Functions {
    public static class FunctionsHelper {
        public static void CheckForDuplicateParameters(TokenLocation loc, IEnumerable<string> pars) {
            var dups = pars
                .GroupBy(x => x)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToArray();

            if (dups.Any()) {
                throw TypeCheckingErrors.IdentifierDefined(loc, dups.First());
            }
        }

        public static void DeclareName(FunctionParseSignature sig, SyntaxFrame types) {
            // Make sure this name isn't taken
            if (types.TryResolvePath(sig.Location.Scope, sig.Name, out _)) {
                throw TypeCheckingErrors.IdentifierDefined(sig.Location, sig.Name);
            }

            // Declare this function
            var path = sig.Location.Scope.Append(sig.Name);

            types.SyntaxValues[path] = new TypeSyntax(sig.Location, new NamedType(path));
        }

        public static void DeclareParameters(TokenLocation loc, FunctionSignature sig, SyntaxFrame types) {
            // Declare the parameters
            for (int i = 0; i < sig.Parameters.Count; i++) {
                var parsePar = sig.Parameters[i];
                var type = sig.Parameters[i].Type;
                var path = sig.Path.Append(parsePar.Name);

                if (parsePar.IsWritable) {
                    type = type.ToMutableType();
                }

                var captured = Array.Empty<IdentifierPath>();
                if (!type.IsValueType(types)) {
                    captured = new[] { sig.Path.Append(parsePar.Name) };
                }

                types.Variables[path] = new VariableSignature(path, type, parsePar.IsWritable, captured);
                types.SyntaxValues[path] = new VariableAccessSyntax(loc, path);
            }
        }
    }
}