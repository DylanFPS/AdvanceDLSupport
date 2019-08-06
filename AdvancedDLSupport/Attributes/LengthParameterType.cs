//
//  LengthParameterType.cs
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
namespace AdvancedDLSupport
{
    /// <summary>
    /// Depicts the used numeric type for length parameters in <see cref="SpanMarshallingAttribute"/>.
    /// </summary>
    public enum LengthParameterType
    {
        /// <summary>
        /// <see cref="byte"/> type.
        /// </summary>
        Byte,

        /// <summary>
        /// <see cref="sbyte"/> type.
        /// </summary>
        SByte,

        /// <summary>
        /// <see cref="short"/> type.
        /// </summary>
        Short,

        /// <summary>
        /// <see cref="ushort"/> type.
        /// </summary>
        UShort,

        /// <summary>
        /// <see cref="int"/> type.
        /// </summary>
        Int,

        /// <summary>
        /// <see cref="uint"/> type.
        /// </summary>
        UInt,

        /// <summary>
        /// <see cref="long"/> type.
        /// </summary>
        Long,

        /// <summary>
        /// <see cref="ulong"/> type.
        /// </summary>
        ULong
    }
}
