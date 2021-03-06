﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (HeadingData.cs) is part of 3P.
// 
// 3P is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// 3P is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with 3P. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
namespace _3PA.Lib.CommonMark.Syntax {
    /// <summary>
    /// Contains additional data for heading elements. Used in the <see cref="Block.Heading"/> property.
    /// </summary>
    public struct HeadingData {
        /// <summary>
        /// Initializes a new instance of the <see cref="HeadingData"/> structure.
        /// </summary>
        /// <param name="level">Heading level.</param>
        public HeadingData(int level) : this() {
            Level = level <= byte.MaxValue ? (byte) level : byte.MaxValue;
        }

        /// <summary>
        /// Gets or sets the heading level.
        /// </summary>
        public byte Level { get; set; }
    }
}