﻿using System.Text;
using System.Text.RegularExpressions;
using Helix.Analysis.TypeChecking;
using Helix.Analysis;
using Helix.Analysis.Types;
using Helix.Generation.CSyntax;
using Helix.Generation.Syntax;

namespace Helix.Generation {
    public interface ICWriter {
        public string GetVariableName();

        public string GetVariableName(IdentifierPath path);

        public void RegisterMemberPath(IdentifierPath varPath, IdentifierPath memberPath);

        public void ResetTempNames();

        public void WriteDeclaration1(ICStatement decl);

        public void WriteDeclaration2(ICStatement decl);

        public void WriteDeclaration3(ICStatement decl);

        public void WriteDeclaration4(ICStatement decl);

        public ICSyntax ConvertType(HelixType type);
    }

    public class CWriter : ICWriter {
        private char tempLetterCounter = 'A';
        private int tempNumberCounter = 0;

        private readonly string header;
        private readonly IDictionary<HelixType, DeclarationCG> typeDeclarations;
        private readonly Dictionary<IdentifierPath, string> pathNames = new();
        private readonly Dictionary<string, int> nameCounters = new();

        private int arrayCounter = 0;
        private readonly Dictionary<string, string> arrayNames = new();
        private readonly Dictionary<string, string> pointerNames = new();

        private readonly StringBuilder decl1Sb = new();
        private readonly StringBuilder decl2Sb = new();
        private readonly StringBuilder decl3Sb = new();
        private readonly StringBuilder decl4Sb = new();

        public CWriter(string header, IDictionary<HelixType, DeclarationCG> typeDecls) {
            this.header = header;
            this.typeDeclarations = typeDecls;
        }

        public string GetVariableName() {
            if (this.tempLetterCounter > 'Z') {
                this.tempLetterCounter = 'A';
                this.tempNumberCounter++;
            }

            if (this.tempNumberCounter > 0) {
                return "$" + this.tempLetterCounter++ + "_" + this.tempNumberCounter;
            }
            else {
                return "$" + this.tempLetterCounter++;
            }
        }

        public void ResetTempNames() {
            this.tempLetterCounter = 'A';
            this.tempNumberCounter = 0;
        }

        public string GetVariableName(IdentifierPath path) {
            if (path.Segments.Count == 1) {
                return path.Segments.First();
            }

            var name = path.Segments.Last();

            if (!this.nameCounters.TryGetValue(name, out int counter)) {
                counter = this.nameCounters[name] = 0;
            }

            if (!this.pathNames.TryGetValue(path, out var value)) {
                value = name;

                if (counter > 0) {
                    value += "_" + counter;
                }

                this.pathNames[path] = value;
                this.nameCounters[name]++;
            }

            return value;
        }

        public void RegisterMemberPath(IdentifierPath varPath, IdentifierPath memberPath) {
            var varName = this.GetVariableName(varPath);

            if (memberPath.Segments.Any()) {
                var path = varPath.Append(memberPath);

                this.pathNames[path] = varName + "." + string.Join(".", memberPath.Segments);
            }
        }

        public void WriteDeclaration1(ICStatement decl) {
            decl.WriteToC(0, this.decl1Sb);
        }

        public void WriteDeclaration2(ICStatement decl) {
            decl.WriteToC(0, this.decl2Sb);
        }

        public void WriteDeclaration3(ICStatement decl) {
            decl.WriteToC(0, this.decl3Sb);
        }

        public void WriteDeclaration4(ICStatement decl) {
            decl.WriteToC(0, this.decl4Sb);
        }

        public ICSyntax ConvertType(HelixType type) {
            // Normalize types by removing dependent types so we dont' have duplicate definitions
            if (type is SingularIntType) {
                return this.ConvertType(PrimitiveType.Int);
            }
            else if (type is SingularBoolType) {
                return this.ConvertType(PrimitiveType.Bool);
            }

            if (type == PrimitiveType.Bool) {
                return new CNamedType("int");
            }
            else if (type == PrimitiveType.Int) {
                return new CNamedType("int");
            }
            else if (type == PrimitiveType.Void) {
                return new CNamedType("int");
            }
            else if (type is PointerType type2) {
                return new CNamedType(this.GeneratePointerType(type2));
            }
            else if (type is NamedType named) {
                if (this.pathNames.TryGetValue(named.Path, out var cname)) {
                    return new CNamedType(cname);
                }                   

                cname = this.pathNames[named.Path] = string.Join("$", named.Path.Segments);

                // Do this after in case we have a recursive struct (through a pointer)
                if (this.typeDeclarations.TryGetValue(type, out var cg)) {
                    cg(this);
                }

                return new CNamedType(cname);
            }
            else if (type is ArrayType array) {
                return new CNamedType(this.GenerateArrayType(array));
            }
            else {
                throw new Exception();
            }
        }

        private string GenerateArrayType(ArrayType arrayType) {
            var inner = this.ConvertType(arrayType.InnerType);
            var innerName = inner.WriteToC();

            if (this.arrayNames.TryGetValue(innerName, out var name)) {
                return name;
            }
            else {
                name = innerName;
            }

            if (Regex.Match(name, @"[a-zA-Z0-9_$]+").Length == 0) {
                name = this.arrayCounter.ToString();
                this.arrayCounter++;
            }

            name += "$array";

            var decl = new CAggregateDeclaration() {
                Name = name,
                Members = new[] { 
                    new CParameter() {
                        Name = "data",
                        Type = new CPointerType(inner)
                    },
                    new CParameter() {
                        Name = "region",
                        Type = this.ConvertType(PrimitiveType.Int)
                    },
                    new CParameter() {
                        Name = "count",
                        Type = this.ConvertType(PrimitiveType.Int)
                    }
                }
            };

            var forwardDecl = new CAggregateDeclaration() {
                Name = name
            };

            this.WriteDeclaration1(forwardDecl);
            this.WriteDeclaration3(decl);
            this.WriteDeclaration3(new CEmptyLine());

            this.arrayNames[innerName] = name; 

            return name;
        }

        private string GeneratePointerType(PointerType pointerType) {
            var inner = this.ConvertType(pointerType.InnerType);
            var innerName = inner.WriteToC();

            if (this.pointerNames.TryGetValue(innerName, out var name)) {
                return name;
            }
            else {
                name = innerName;
            }

            if (Regex.Match(name, @"[a-zA-Z0-9_$]+").Length == 0) {
                name = this.arrayCounter.ToString();
                this.arrayCounter++;
            }

            name += "$ptr";

            var decl = new CAggregateDeclaration() {
                Name = name,
                Members = new[] {
                    new CParameter() {
                        Name = "data",
                        Type = new CPointerType(inner)
                    },
                    new CParameter() {
                        Name = "region",
                        Type = this.ConvertType(PrimitiveType.Int)
                    }
                }
            };

            var forwardDecl = new CAggregateDeclaration() {
                Name = name
            };

            this.WriteDeclaration1(forwardDecl);
            this.WriteDeclaration3(decl);
            this.WriteDeclaration3(new CEmptyLine());

            this.pointerNames[innerName] = name;

            return name;
        }

        public override string ToString() {
            var sb = new StringBuilder();

            sb.AppendLine("#if __cplusplus");
            sb.AppendLine("extern \"C\" {");
            sb.AppendLine("#endif").AppendLine();

            sb.AppendLine(this.header);

            if (this.decl1Sb.Length > 0) {
                sb.Append(this.decl1Sb).AppendLine();
            }

            if (this.decl2Sb.Length > 0) {
                sb.Append(this.decl2Sb).AppendLine();
            }

            if (this.decl3Sb.Length > 0) {
                sb.Append(this.decl3Sb);
            }

            if (this.decl4Sb.Length > 0) {
                sb.Append(this.decl4Sb);
            }

            sb.AppendLine("#if __cplusplus");
            sb.AppendLine("}");
            sb.AppendLine("#endif");

            return sb.ToString();
        }
    }
}