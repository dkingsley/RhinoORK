// MIT License
// 
// Copyright (c) 2017 Dennis Kingsley
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;

namespace RhinoORK.Geometry
{
	/// <summary>
    /// Class definition of an Haack curve, contant == 1/3 for LV-Haack (LV indicates minimum drag for a given length and volume),
    /// and constant == 0 for LD-Haack or Von Kármán (LD signifies minimum drag for the given length and diameter)
	/// </summary>
	public class HaackCurve
	{
		private double _radiusBase;
        private double _length;
        private double _constant;

        /// <summary>
        /// Constructor for a Haack Series curve
        /// </summary>
        /// <param name="radius">The radius of the base</param>
        /// <param name="length">the length of the curve</param>
        /// <param name="constant">constant see class summary for details.</param>
        public HaackCurve(double radius, double length, double constant)
        {
            _radiusBase = radius;
            _length = length;
            _constant = constant;
        }

        /// <summary>
        /// evaluate the arc curve and return the value at x.
        /// </summary>
        /// <param name="x">the location along the curve to evaluate at.</param>
        /// <returns>the width of the parabola at x.</returns>
		public double Evaluate(double x)
		{
            double theta = Math.Acos(1.0 - 2.0 * x / _length);

            double tmp = theta - Math.Sin(2 * theta) / 2 + _constant * Math.Pow(Math.Sin(theta), 3);
            double y = _radiusBase / Math.Sqrt(Math.PI) * Math.Sqrt(tmp);
            return y;
		}
	}
}
