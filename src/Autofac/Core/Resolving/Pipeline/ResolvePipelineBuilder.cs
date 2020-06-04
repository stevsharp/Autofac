﻿// This software is part of the Autofac IoC container
// Copyright © 2011 Autofac Contributors
// https://autofac.org
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using Autofac.Core.Pipeline;
using Autofac.Core.Resolving.Middleware;

namespace Autofac.Core.Resolving.Pipeline
{
    /// <summary>
    /// Provides the functionality to construct a resolve pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pipeline builder is built as a doubly-linked list; each node in that list is a
    /// <see cref="MiddlewareDeclaration"/>, that holds the middleware instance, and the reference to the next and previous nodes.
    /// </para>
    ///
    /// <para>
    /// When you call one of the Use* methods, we find the appropriate node in the linked list based on the phase of the new middleware
    /// and insert it into the list.
    /// </para>
    ///
    /// <para>
    /// When you build a pipeline, we walk back through that set of middleware and generate the concrete call chain so that when you execute the pipeline,
    /// we don't iterate over any nodes, but just invoke the built set of methods.
    /// </para>
    /// </remarks>
    internal class ResolvePipelineBuilder : IResolvePipelineBuilder, IEnumerable<IResolveMiddleware>
    {
        /// <summary>
        /// Termination action for the end of pipelines, that will execute the specified continuation (if there is one).
        /// </summary>
        private static readonly Action<ResolveRequestContextBase> _terminateAction = ctxt => ctxt.Continuation?.Invoke(ctxt);

        private const string AnonymousName = "unnamed";

        private MiddlewareDeclaration? _first;
        private MiddlewareDeclaration? _last;

        /// <inheritdoc/>
        public IEnumerable<IResolveMiddleware> Middleware => this;

        /// <inheritdoc/>
        public IResolvePipelineBuilder Use(IResolveMiddleware stage, MiddlewareInsertionMode insertionMode = MiddlewareInsertionMode.EndOfPhase)
        {
            if (stage is null)
            {
                throw new ArgumentNullException(nameof(stage));
            }

            AddStage(stage, insertionMode);

            return this;
        }

        /// <inheritdoc/>
        public IResolvePipelineBuilder Use(PipelinePhase phase, Action<ResolveRequestContextBase, Action<ResolveRequestContextBase>> callback)
        {
            Use(phase, MiddlewareInsertionMode.EndOfPhase, callback);

            return this;
        }

        /// <inheritdoc/>
        public IResolvePipelineBuilder Use(PipelinePhase phase, MiddlewareInsertionMode insertionMode, Action<ResolveRequestContextBase, Action<ResolveRequestContextBase>> callback)
        {
            Use(AnonymousName, phase, insertionMode, callback);

            return this;
        }

        /// <inheritdoc/>
        public IResolvePipelineBuilder Use(string name, PipelinePhase phase, Action<ResolveRequestContextBase, Action<ResolveRequestContextBase>> callback)
        {
            Use(new DelegateMiddleware(name, phase, callback), MiddlewareInsertionMode.EndOfPhase);

            return this;
        }

        /// <inheritdoc/>
        public IResolvePipelineBuilder Use(string name, PipelinePhase phase, MiddlewareInsertionMode insertionMode, Action<ResolveRequestContextBase, Action<ResolveRequestContextBase>> callback)
        {
            Use(new DelegateMiddleware(name, phase, callback), insertionMode);

            return this;
        }

        /// <inheritdoc/>
        public IResolvePipelineBuilder UseRange(IEnumerable<IResolveMiddleware> stages, MiddlewareInsertionMode insertionMode = MiddlewareInsertionMode.EndOfPhase)
        {
            // Use multiple stages.
            // Start at the beginning.
            var currentStage = _first;
            using var enumerator = stages.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                return this;
            }

            var nextNewStage = enumerator.Current;
            var lastPhase = nextNewStage.Phase;

            while (currentStage is object)
            {
                if (insertionMode == MiddlewareInsertionMode.StartOfPhase ?
                        currentStage.Middleware.Phase >= nextNewStage.Phase :
                        currentStage.Middleware.Phase > nextNewStage.Phase)
                {
                    var newDecl = new MiddlewareDeclaration(enumerator.Current);

                    if (currentStage.Previous is object)
                    {
                        // Insert the node.
                        currentStage.Previous.Next = newDecl;
                        newDecl.Next = currentStage;
                        newDecl.Previous = currentStage.Previous;
                        currentStage.Previous = newDecl;
                    }
                    else
                    {
                        _first!.Previous = newDecl;
                        newDecl.Next = _first;
                        _first = newDecl;
                    }

                    currentStage = newDecl;

                    if (!enumerator.MoveNext())
                    {
                        // Done.
                        return this;
                    }

                    nextNewStage = enumerator.Current;

                    if (nextNewStage.Phase < lastPhase)
                    {
                        throw new InvalidOperationException(ResolvePipelineBuilderMessages.MiddlewareMustBeInPhaseOrder);
                    }

                    lastPhase = nextNewStage.Phase;
                }

                currentStage = currentStage.Next;
            }

            do
            {
                nextNewStage = enumerator.Current;

                if (nextNewStage.Phase < lastPhase)
                {
                    throw new InvalidOperationException(ResolvePipelineBuilderMessages.MiddlewareMustBeInPhaseOrder);
                }

                lastPhase = nextNewStage.Phase;

                var newStageDecl = new MiddlewareDeclaration(nextNewStage);

                if (_last is null)
                {
                    _first = _last = newStageDecl;
                }
                else
                {
                    newStageDecl.Previous = _last;
                    _last.Next = newStageDecl;
                    _last = newStageDecl;
                }
            }
            while (enumerator.MoveNext());

            return this;
        }

        private void AddStage(IResolveMiddleware stage, MiddlewareInsertionMode insertionLocation)
        {
            // Start at the beginning.
            var currentStage = _first;

            var newStageDecl = new MiddlewareDeclaration(stage);

            if (_first is null)
            {
                _first = _last = newStageDecl;
                return;
            }

            while (currentStage is object)
            {
                if (insertionLocation == MiddlewareInsertionMode.StartOfPhase ? currentStage.Middleware.Phase >= stage.Phase : currentStage.Middleware.Phase > stage.Phase)
                {
                    if (currentStage.Previous is object)
                    {
                        // Insert the node.
                        currentStage.Previous.Next = newStageDecl;
                        newStageDecl.Next = currentStage;
                        newStageDecl.Previous = currentStage.Previous;
                        currentStage.Previous = newStageDecl;
                    }
                    else
                    {
                        _first.Previous = newStageDecl;
                        newStageDecl.Next = _first;
                        _first = newStageDecl;
                    }

                    return;
                }

                currentStage = currentStage.Next;
            }

            // Add at the end.
            newStageDecl.Previous = _last;
            _last!.Next = newStageDecl;
            _last = newStageDecl;
        }

        private void AppendStage(IResolveMiddleware stage)
        {
            var newDecl = new MiddlewareDeclaration(stage);

            if (_last is null)
            {
                _first = _last = newDecl;
            }
            else
            {
                newDecl.Previous = _last;
                _last.Next = newDecl;
                _last = newDecl;
            }
        }

        /// <inheritdoc />
        public IResolvePipeline Build()
        {
            return BuildPipeline(_last);
        }

        private static IResolvePipeline BuildPipeline(MiddlewareDeclaration? lastDecl)
        {
            // When we build, we go through the set and construct a single call stack, starting from the end.
            var current = lastDecl;
            Action<ResolveRequestContextBase>? currentInvoke = _terminateAction;

            Action<ResolveRequestContextBase> Chain(Action<ResolveRequestContextBase> next, IResolveMiddleware stage)
            {
                return (ctxt) =>
                {
                    // Optimise the path depending on whether a tracer is attached.
                    if (ctxt.TracingEnabled)
                    {
                        ctxt.Tracer!.MiddlewareEntry(ctxt.Operation, ctxt, stage);
                        var succeeded = false;
                        try
                        {
                            ctxt.PhaseReached = stage.Phase;
                            stage.Execute(ctxt, next);
                            succeeded = true;
                        }
                        finally
                        {
                            ctxt.Tracer.MiddlewareExit(ctxt.Operation, ctxt, stage, succeeded);
                        }
                    }
                    else
                    {
                        ctxt.PhaseReached = stage.Phase;
                        stage.Execute(ctxt, next);
                    }
                };
            }

            while (current is object)
            {
                var stage = current.Middleware;
                currentInvoke = Chain(currentInvoke, stage);
                current = current.Previous;
            }

            return new ResolvePipeline(currentInvoke);
        }

        /// <inheritdoc/>
        public IResolvePipelineBuilder Clone()
        {
            // To clone a pipeline, we create a new instance, then insert the same stage
            // objects in the same order.
            var newPipeline = new ResolvePipelineBuilder();
            var currentStage = _first;

            while (currentStage is object)
            {
                newPipeline.AppendStage(currentStage.Middleware);
                currentStage = currentStage.Next;
            }

            return newPipeline;
        }

        /// <inheritdoc/>
        public IEnumerator<IResolveMiddleware> GetEnumerator()
        {
            return new PipelineBuilderEnumerator(_first);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
