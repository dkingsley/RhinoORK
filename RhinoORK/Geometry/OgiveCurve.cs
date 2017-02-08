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
	/// Class definition of an ogive curve
	/// </summary>
	public class OgiveCurve
	{
		private double _radiusBase;
		private double _lengthCurve;
		private double _rho;

		public OgiveCurve(double radius, double length)
		{
			_radiusBase = radius;
			_lengthCurve = length;

			_rho = (Math.Pow(_radiusBase, 2) + Math.Pow(_lengthCurve, 2)) / (2.0 * _radiusBase);
		}

		public double Evaluate(double x)
		{
			double tmp1 = Math.Pow(_rho, 2) - Math.Pow(_lengthCurve - x, 2);
			return Math.Sqrt(tmp1) + _radiusBase - _rho;
		}

		public double EvaluateSphericalCap(double x, double rn)
		{
			double x0 = _lengthCurve - Math.Sqrt(Math.Pow(_rho - rn, 2) - Math.Pow(_rho - _radiusBase, 2));
			double yt = rn * (_rho - _radiusBase) / (_rho - rn);
			double xt = x0 - Math.Sqrt(Math.Pow(rn, 2) - Math.Pow(yt, 2));

			double y = 0;
			if (x >= xt)
			{
				double tmp1 = Math.Pow(_rho, 2) - Math.Pow(_lengthCurve - x, 2);
				y = Math.Sqrt(tmp1) + _radiusBase - _rho;
			}
			else
			{
				double xa = SphericalCapApex(rn);

				if (x >= xa)
					y = Math.Sqrt(Math.Pow(rn, 2) - Math.Pow(x - (xa + rn), 2));
			}

			return y;
		}

		public double SphericalCapApex(double rn)
		{
			double x0 = _lengthCurve - Math.Sqrt(Math.Pow(_rho - rn, 2) - Math.Pow(_rho - _radiusBase, 2));
			double yt = rn * (_rho - _radiusBase) / (_rho - rn);
			double xt = x0 - Math.Sqrt(Math.Pow(rn, 2) - Math.Pow(yt, 2));

			double xa = x0 - rn;

			return xa;
		}
	}
}
