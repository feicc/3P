﻿#region header

// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ListDelimiter.cs) is part of 3P.
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
    /// Defines the delimiter used in the source for ordered lists.
    /// </summary>
    public enum ListDelimiter : byte {
        /// <summary>
        /// The item numbering is followed with a period (<c>1. foo</c>).
        /// </summary>
        Period = 0,

        /// <summary>
        /// The item numbering is followed with a closing parenthesis (<c>1) foo</c>).
        /// </summary>
        Parenthesis
    }
}