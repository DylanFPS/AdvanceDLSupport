//
//  SpanMarshallingAttribute.cs
//
//  Copyright (c) 2018 Firwood Software
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using JetBrains.Annotations;

namespace AdvancedDLSupport
{
    /// <summary>
    /// Holds metadata for native functions.
    /// </summary>
    [PublicAPI, AttributeUsage(AttributeTargets.Parameter)]
    public class SpanMarshallingAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the direction in which the length parameter should be placed.
        /// </summary>
        [PublicAPI]
        public LengthParameterDirection LengthParameterDirection { get; set; } = LengthParameterDirection.After;

        /// <summary>
        /// Gets or sets the offset at which the length parameter should be placed in
        /// <see cref="LengthParameterDirection"/> direction.
        /// </summary>
        [PublicAPI]
        public int LengthParameterOffset { get; set; }

        /// <summary>
        /// Gets or sets the offset at which the length parameter should be placed in
        /// <see cref="LengthParameterDirection"/> direction.
        /// </summary>
        [PublicAPI]
        public LengthParameterType LengthParameterType { get; set; } = LengthParameterType.Int;
    }
}
