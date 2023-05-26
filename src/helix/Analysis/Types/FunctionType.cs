﻿using Helix.Analysis;
using Helix.Analysis.TypeChecking;
using Helix.Analysis.Types;

namespace Helix.Features.Types {
    public record FunctionType : HelixType {
        public HelixType ReturnType { get; }

        public IReadOnlyList<FunctionParameter> Parameters { get; }

        public FunctionType(HelixType returnType, IReadOnlyList<FunctionParameter> pars) {
            this.ReturnType = returnType;
            this.Parameters = pars;
        }

        public override PassingSemantics GetSemantics(ITypeContext types) {
            return PassingSemantics.ReferenceType;
        }

        public override HelixType GetMutationSupertype(ITypeContext types) => this;

        public override HelixType GetSignatureSupertype(ITypeContext types) => this;
    }

    public record FunctionParameter {
        public string Name { get; }

        public HelixType Type { get; }

        public bool IsWritable { get; }

        public FunctionParameter(string name, HelixType type, bool isWritable) {
            this.Name = name;
            this.Type = type;
            this.IsWritable = isWritable;
        }
    }
}