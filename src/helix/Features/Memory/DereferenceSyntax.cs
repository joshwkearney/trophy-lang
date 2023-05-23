﻿using Helix.Analysis;
using Helix.Analysis.Flow;
using Helix.Analysis.TypeChecking;
using Helix.Syntax;
using Helix.Analysis.Types;
using Helix.Features.Memory;
using Helix.Generation;
using Helix.Generation.Syntax;
using Helix.Parsing;
using Helix.Collections;

namespace Helix.Parsing {
    public partial class Parser {
        private int dereferenceCounter = 0;

        public ISyntaxTree DereferenceExpression(ISyntaxTree first) {
            var op = this.Advance(TokenKind.Star);
            var loc = first.Location.Span(op.Location);

            return new DereferenceSyntax(
                loc, 
                first, 
                this.scope.Append("$deref_" + this.dereferenceCounter++));
        }
    }
}

namespace Helix.Features.Memory {
    // Dereference syntax is split into three classes: this one that does
    // some basic type checking so it's easy for the parser to spit out
    // a single class, a dereference rvalue, and a dereference lvaulue.
    // This is for clarity because dereference rvalues and lvalues have
    // very different semantics, especially when it comes to lifetimes
    public record DereferenceSyntax : ISyntaxTree {
        private readonly ISyntaxTree target;
        private readonly IdentifierPath tempPath;

        public TokenLocation Location { get; }

        public IEnumerable<ISyntaxTree> Children => new[] { this.target };

        public bool IsPure => this.target.IsPure;

        public DereferenceSyntax(TokenLocation loc, ISyntaxTree target,
                                 IdentifierPath tempPath) {
            this.Location = loc;
            this.target = target;
            this.tempPath = tempPath;
        }

        public Option<HelixType> AsType(TypeFrame types) {
            return this.target.AsType(types)
                .Select(x => new PointerType(x))
                .Select(x => (HelixType)x);
        }

        public ISyntaxTree CheckTypes(TypeFrame types) {
            if (this.IsTypeChecked(types)) {
                return this;
            }

            var target = this.target.CheckTypes(types).ToRValue(types);
            var pointerType = target.AssertIsPointer(types);
            var result = new DereferenceSyntax(this.Location, target, this.tempPath);

            result.SetReturnType(pointerType.InnerType, types);
            result.SetCapturedVariables(target, types);

            return result;
        }

        public ISyntaxTree ToRValue(TypeFrame types) {
            if (!this.IsTypeChecked(types)) {
                throw new InvalidOperationException();
            }

            return new DereferenceRValue(this.Location, this.target, this.tempPath).CheckTypes(types);
        }

        public ISyntaxTree ToLValue(TypeFrame types) {
            if (!this.IsTypeChecked(types)) {
                throw new InvalidOperationException();
            }

            return new DereferenceLValue(this.Location, this.target, this.tempPath).CheckTypes(types);
        }
    }

    public record DereferenceRValue : ISyntaxTree {
        private readonly ISyntaxTree target;
        private readonly IdentifierPath tempPath;

        public TokenLocation Location { get; }

        public IEnumerable<ISyntaxTree> Children => new[] { this.target };

        public bool IsPure => this.target.IsPure;

        public DereferenceRValue(
            TokenLocation loc, 
            ISyntaxTree target, 
            IdentifierPath tempPath) {

            this.Location = loc;
            this.target = target;
            this.tempPath = tempPath;
        }

        public Option<HelixType> AsType(TypeFrame types) {
            return this.target.AsType(types)
                .Select(x => new PointerType(x))
                .Select(x => (HelixType)x);
        }

        public ISyntaxTree CheckTypes(TypeFrame types) {
            if (this.IsTypeChecked(types)) {
                return this;
            }

            // this.target is already type checked
            var pointerType = this.target.AssertIsPointer(types);

            this.SetReturnType(pointerType.InnerType, types);
            this.SetCapturedVariables(this.target, types);

            return this;
        }

        public void AnalyzeFlow(FlowFrame flow) {
            if (this.IsFlowAnalyzed(flow)) {
                return;
            }

            this.target.AnalyzeFlow(flow);

            var pointerType = this.target.AssertIsPointer(flow);
            var bundleDict = new Dictionary<IdentifierPath, LifetimeBounds>();

            // Doing this is ok because pointers don't have members
            var pointerLifetime = this.target.GetLifetimes(flow)[new IdentifierPath()];

            // Build a return bundle composed of lifetimes that outlive the pointer's lifetime
            // This loop replaces flow.DeclareValueLifetimes() because some custom logic is needed
            foreach (var (relPath, type) in pointerType.InnerType.GetMembers(flow)) {
                var memPath = this.tempPath.AppendMember(relPath);

                if (type.IsValueType(flow)) {
                    bundleDict[relPath] = new LifetimeBounds(Lifetime.None);
                    flow.LocalLifetimes = flow.LocalLifetimes.SetItem(memPath, new LifetimeBounds());

                    continue;
                }

                // If we are dereferencing a pointer and the following three conditions hold,
                // we don't have to make up a new lifetime: 1) We're dereferencing a local variable
                // 2) That local variable could not have been mutated by an alias since the last 
                // time it was set 3) That local variable is storing the location of another variable
                if (pointerLifetime.LocationLifetime != Lifetime.None) {
                    var valueLifetime = flow.LocalLifetimes[pointerLifetime.LocationLifetime.Path].ValueLifetime;

                    var equivalents = flow
                        .LifetimeGraph
                        .GetEquivalentLifetimes(valueLifetime)
                        .Where(x => x.Origin == LifetimeOrigin.LocalLocation);

                    // If all three are true, we can return the location of the that variable
                    // whose location is currently stored in the variable we're dereferencing.
                    // Think of this as optimizing dereferencing an addressof operator.
                    if (equivalents.Any()) {
                        bundleDict[relPath] = new LifetimeBounds(equivalents.First());

                        continue;
                    }
                }

                // This value's lifetime actually isn't the pointer's lifetime, but some
                // other lifetime that outlives the pointer. It's important to represent
                // the value like this because we can't store things into it that only
                // outlive the pointer
                var derefValueLifetime = new ValueLifetime(memPath, LifetimeRole.Root, LifetimeOrigin.TempValue);

                // Make sure we add this as a root
                flow.LifetimeRoots = flow.LifetimeRoots.Add(derefValueLifetime);

                // The lifetime that is stored in the pointer must outlive the pointer itself
                flow.LifetimeGraph.AddStored(derefValueLifetime, pointerLifetime.ValueLifetime, this.GetReturnType(flow));

                bundleDict[relPath] = new LifetimeBounds(derefValueLifetime);
            }

            this.SetLifetimes(new LifetimeBundle(bundleDict), flow);
        }

        public ICSyntax GenerateCode(FlowFrame types, ICStatementWriter writer) {
            var target = this.target.GenerateCode(types, writer);
            var pointerType = this.target.AssertIsPointer(types);
            var tempName = writer.GetVariableName(this.tempPath);
            var tempType = writer.ConvertType(pointerType.InnerType);

            writer.WriteEmptyLine();
            writer.WriteComment($"Line {this.Location.Line}: Pointer dereference");
            writer.WriteStatement(new CVariableDeclaration() {
                Name = tempName,
                Type = tempType,
                Assignment = new CPointerDereference() {
                    Target = new CMemberAccess() {
                        Target = target,
                        MemberName = "data",
                        IsPointerAccess = false
                    }
                }
            });

            writer.WriteEmptyLine();
            return new CVariableLiteral(tempName);
        }
    }

    public record DereferenceLValue : ISyntaxTree {
        private readonly ISyntaxTree target;
        private readonly IdentifierPath tempPath;

        public TokenLocation Location { get; }

        public IEnumerable<ISyntaxTree> Children => new[] { this.target };

        public bool IsPure => this.target.IsPure;

        public DereferenceLValue(TokenLocation loc, ISyntaxTree target, IdentifierPath tempPath) {
            this.Location = loc;
            this.target = target;
            this.tempPath = tempPath;
        }

        public ISyntaxTree CheckTypes(TypeFrame types) {
            if (this.IsTypeChecked(types)) {
                return this;
            }

            this.SetReturnType(this.target.GetReturnType(types), types);
            this.SetCapturedVariables(this.target, types);

            return this;
        }

        public ISyntaxTree ToLValue(TypeFrame types) {
            if (!this.IsTypeChecked(types)) {
                throw new InvalidOperationException();
            }

            return this;
        }

        public void AnalyzeFlow(FlowFrame flow) {
            if (this.IsFlowAnalyzed(flow)) {
                return;
            }

            this.target.AnalyzeFlow(flow);            

            var targetBounds = this.target.GetLifetimes(flow)[new IdentifierPath()];
            var dict = new Dictionary<IdentifierPath, LifetimeBounds>();

            // If we are dereferencing a pointer and the following three conditions hold,
            // we don't have to make up a new lifetime: 1) We're dereferencing a local variable
            // 2) That local variable is storing the location of another variable
            if (this.AnalyzeLocalDeref(targetBounds, flow)) {
                return;
            }

            var memPath = this.tempPath.ToVariablePath();

            var precursors = flow.LifetimeGraph
                .GetPrecursorLifetimes(targetBounds.ValueLifetime)
                .ToArray();

            var derefValueLifetime = new ValueLifetime(
                    memPath,
                    LifetimeRole.Alias,
                    LifetimeOrigin.TempValue);

            // We could potentially be storing into anything upstream of our target
            // with pointer aliasing, so assume that is the case and add the correct
            // dependencies
            foreach (var root in precursors) {
                flow.LifetimeGraph.AddStored(derefValueLifetime, root, null);
            }

            // The lifetime that is stored in the pointer must outlive the pointer itself
            flow.LifetimeGraph.AddStored(derefValueLifetime, targetBounds.ValueLifetime, this.GetReturnType(flow));

            dict[new IdentifierPath()] = new LifetimeBounds(derefValueLifetime, targetBounds.ValueLifetime);
            this.SetLifetimes(new LifetimeBundle(dict), flow);
        }

        private bool AnalyzeLocalDeref(LifetimeBounds targetBounds, FlowFrame flow) {
            if (targetBounds.LocationLifetime == Lifetime.None) {
                return false;
            }

            var valueLifetime = flow.LocalLifetimes[targetBounds.LocationLifetime.Path].ValueLifetime;
            var dict = new Dictionary<IdentifierPath, LifetimeBounds>();

            var equivalents = flow
                .LifetimeGraph
                .GetEquivalentLifetimes(valueLifetime)
                .Where(x => x.Origin == LifetimeOrigin.LocalLocation); ;

            // If all three are true, we can return the location of the that variable
            // whose location is currently stored in the variable we're dereferencing.
            // Think of this as optimizing dereferencing an addressof operator.
            if (!equivalents.Any()) {
                return false;
            }

            var loc = equivalents.First();
            var value = flow.LocalLifetimes[loc.Path].ValueLifetime;

            dict[new IdentifierPath()] = new LifetimeBounds(value, loc);
            this.SetLifetimes(new LifetimeBundle(dict), flow);

            return true;
        }

        public ICSyntax GenerateCode(FlowFrame types, ICStatementWriter writer) {
            var target = this.target.GenerateCode(types, writer);
            var result = new CMemberAccess() {
                Target = target,
                MemberName = "data"
            };

            return result;
        }
    }
}
