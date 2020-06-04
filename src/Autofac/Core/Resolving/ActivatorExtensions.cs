﻿// This software is part of the Autofac IoC container
// Copyright © 2019 Autofac Contributors
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

using Autofac.Core.Activators.Delegate;

namespace Autofac.Core.Resolving
{
    /// <summary>
    /// Extension methods for activators.
    /// </summary>
    internal static class ActivatorExtensions
    {
        /// <summary>
        /// This shorthand name for the activator is used in exception messages; for activator types
        /// where the limit type generally describes the activator exactly, we use that; for delegate
        /// activators, a variation on the type name is used to indicate this.
        /// </summary>
        /// <param name="activator">The activator instance.</param>
        /// <returns>A display name.</returns>
        public static string DisplayName(this IInstanceActivator activator)
        {
            var fullName = activator.LimitType.FullName ?? "";
            return activator is DelegateActivator ?
                $"λ:{fullName}" :
                fullName;
        }
    }
}
