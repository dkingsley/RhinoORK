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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Windows.Forms;

using Rhino;
using Rhino.Geometry;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;

using RhinoORK.Geometry;

namespace RhinoORK
{
  [System.Runtime.InteropServices.Guid("FE4BA664-3D8A-4396-AACF-C9E4C54B86BD")]
  public class RhinoORK_ImportORK : Command
  {
    public override string EnglishName
    {
        get { return "RhinoORK_ImportORK"; }
    }

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        Result result = Result.Failure;
        
        OpenFileDialog openFileDlg = new OpenFileDialog();
        openFileDlg.Filter = "OpenRocket files (*.ork)|*.ork|All files (*.*)|*.*";
        openFileDlg.InitialDirectory = doc.Path;
        openFileDlg.Title = "Please select a OpenRocket file to Import.";

        if (openFileDlg.ShowDialog() == DialogResult.OK)
        {
            XmlDocument xmlDoc = new XmlDocument();

            using (ZipArchive archive = ZipFile.Open(openFileDlg.FileName, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.Name == "rocket.ork")
                        xmlDoc.Load(entry.Open());
                }
            }

            XmlElement root = xmlDoc.DocumentElement;

            XmlNodeList rocketNds = root.GetElementsByTagName("rocket");

            if (rocketNds.Count == 0)
                return Result.Failure;

            XmlElement rocketEle = rocketNds[0] as XmlElement;

            XmlNodeList compNds = rocketNds[0].SelectNodes("/openrocket/rocket/subcomponents/stage/subcomponents/*");
                       
            double stackLength = 0;

            foreach (XmlNode compNd in compNds)
            {
                if (compNd.Name == "nosecone")
                    result = CreateNoseCone(doc, compNd, ref stackLength);
                else if (compNd.Name == "bodytube")
                    result = CreateBodyTube(doc, compNd, ref stackLength);
            }
        }

        return result;
    }

    public enum NoseConeShapeType
    {
        /// <summary>
        /// Use the ogive curve to define the nose cone
        /// </summary>
        Ogive,
        /// <summary>
        /// Use the Haack Series curve which is mathematically derived for the purpose of minimizing drag; 
        /// </summary>
        Haack,
        /// <summary>
        /// use a cone to define a nose cone
        /// </summary>
        Cone,
        /// <summary>
        /// use a parabola to define a nose cone
        /// </summary>
        Parabola,
        /// <summary>
        /// use a half an elipse to define a nose cone
        /// </summary>
        Elliptic,
        /// <summary>
        /// use a half a circle to define a nose cone
        /// </summary>
        Circular
    }
    public enum PositionType
    {
        Top,
        Middle,
        Bottom,
        After,
        Absolute
    }

    public enum RelativePositionType
    {
        Front,
        Center,
        End,
    }
    private Result CreateFreeFormFin(RhinoDoc doc, XmlNode compNd, double parentRadius, double xStart, double xEnd)
    {
        Result result = Rhino.Commands.Result.Failure;
        XmlElement compEle = compNd as XmlElement;
        XmlNodeList subCompNds = compNd.SelectNodes("subcomponents/*");

        int count = 0;
        PositionType positionType = PositionType.Top;
        double position = 0;
        double rotation = 0;
        double cant = 0;
        double thickness = 0;
        double tabHeight = 0;
        double tabLength = 0;
        RelativePositionType tabPositionType = RelativePositionType.Front;
        double tabPosition=0;
        List<Point3d> points = new List<Point3d>();

        foreach (XmlNode nd in compEle.ChildNodes)
        {
            if (nd.Name == "fincount")
                count = int.Parse(nd.InnerText);
            else if (nd.Name == "thickness")
                thickness = Double.Parse(nd.InnerText);
            else if (nd.Name == "rotation")
                rotation = Double.Parse(nd.InnerText);
            else if  (nd.Name == "position")
            {
                position = Double.Parse(nd.InnerText);
                positionType = (PositionType)Enum.Parse(typeof(PositionType), ((XmlElement)nd).GetAttribute("type"), true);
            }
            else if (nd.Name == "cant")
                cant = Double.Parse(nd.InnerText);
             else if (nd.Name == "tabheight")
                tabHeight = Double.Parse(nd.InnerText);
            else if (nd.Name == "tablength")
                tabLength = Double.Parse(nd.InnerText);
            else if  (nd.Name == "tabposition")
            {
                tabPosition = Double.Parse(nd.InnerText);
                tabPositionType = (RelativePositionType)Enum.Parse(typeof(RelativePositionType), ((XmlElement)nd).GetAttribute("relativeto"), true);
            }
            else if  (nd.Name == "finpoints")
            {
                foreach(XmlNode ptNd in nd.ChildNodes)
                {
                    XmlElement ptEle = ptNd as XmlElement;
                    double x = Double.Parse(ptEle.GetAttribute("x"));
                    double y = Double.Parse(ptEle.GetAttribute("y"));
                    points.Add( new Point3d(x,y,0.0));
                }
            }
        }

        double rootFinLength = points[points.Count-1].X - points[0].X;
        if (tabHeight*tabLength>0)
        {
            List<Point3d> tabPts = new List<Point3d>(); // add points from aft end of fin.
            double remainingLength = rootFinLength - tabLength;
            if (remainingLength <0)
            {
                tabLength = rootFinLength;
                remainingLength = 0;
            }

            switch(tabPositionType)
            {
                case RelativePositionType.Front:
                    tabPts.Add(new Point3d(points[0].X + tabLength, 0, 0));
                    tabPts.Add(new Point3d(points[0].X + tabLength, -tabHeight, 0));
                    tabPts.Add(new Point3d(points[0].X,-tabHeight, 0));
                    tabPts.Add(new Point3d(points[0].X, 0, 0));
                    break;
                case RelativePositionType.Center:
                    remainingLength /= 2;
                    tabPts.Add(new Point3d(points[0].X + tabLength + remainingLength, 0, 0));
                    tabPts.Add(new Point3d(points[0].X + tabLength + remainingLength, -tabHeight, 0));
                    tabPts.Add(new Point3d(points[0].X + remainingLength, -tabHeight, 0));
                    tabPts.Add(new Point3d(points[0].X + remainingLength, 0, 0));
                    tabPts.Add(new Point3d(points[0].X, 0, 0));
                    break;
                case RelativePositionType.End:
                    tabPts.Add(new Point3d(points[0].X + tabLength, 0, 0));
                    tabPts.Add(new Point3d(points[0].X + tabLength, -tabHeight, 0));
                    tabPts.Add(new Point3d(points[0].X + remainingLength, -tabHeight, 0));
                    tabPts.Add(new Point3d(points[0].X + remainingLength, 0, 0));

                    break;
            }
            points.AddRange(tabPts);
        }

        Brep brep = null;

        Polyline curve = new Polyline(points);

        double xLoc = 0;
        switch (positionType)
        {
            case PositionType.Top:
                xLoc = xStart + position;
                break;
            case PositionType.Bottom:
                xLoc = xEnd-rootFinLength + position;
                break;
            case PositionType.Middle:
                xLoc = (xEnd - xStart) / 2 + position;
                break;
            case PositionType.After:
                xLoc = xEnd + position;
                break;
            case PositionType.Absolute:
                xLoc = position;
                break;
        }

        List<Brep> breps = new List<Brep>();

        if (curve.IsClosed)
        {
            Extrusion ext = Extrusion.Create(curve.ToNurbsCurve(), thickness, true);
            brep = ext.ToBrep();

            Transform trans = Transform.Translation(new Vector3d(0, parentRadius, thickness/2));
            brep.Transform(trans);
            trans = Transform.Rotation(cant*Math.PI/180.0, Vector3d.YAxis, new Point3d(rootFinLength / 2, 0, 0));
            brep.Transform(trans);
            trans = Transform.Translation(new Vector3d(xLoc, 0, 0));
            brep.Transform(trans);
            breps.Add(brep);

            double deltaAng = 360 / count;

            for (int i=1; i<count; i++)
            {
                Transform copyTrans = Transform.Rotation(i*deltaAng * Math.PI / 180.0, Vector3d.XAxis, new Point3d(0, 0, 0));
                Brep newBrep = brep.DuplicateBrep();
                newBrep.Transform(copyTrans);
                breps.Add(newBrep);
            }

            foreach (Brep brp in breps)
            {
                if (doc.Objects.AddBrep(brp) != Guid.Empty)
                {
                    result = Rhino.Commands.Result.Success;
                }
            }

            doc.Views.Redraw();
        }

        return result;
    }

    private Result CreateBodyTube(RhinoDoc doc, XmlNode compNd, ref double stackLength)
    {
        Result result = Rhino.Commands.Result.Failure;
        XmlElement compEle = compNd as XmlElement;
        XmlNodeList subCompNds = compNd.SelectNodes("subcomponents/*");

        double length = 0;
        double thickness = 0;
        double radius = 0;
  
        foreach (XmlNode nd in compEle.ChildNodes)
        {
            if (nd.Name == "length")
                length = Double.Parse(nd.InnerText);
            else if (nd.Name == "thickness")
                thickness = Double.Parse(nd.InnerText);
           else if (nd.Name == "radius")
            {
                if (nd.InnerText == "auto")
                {
                    bool found = false;
                    
                    foreach (XmlNode sibNd in compNd.ParentNode.ChildNodes)
                    {
                        foreach (XmlNode sibChild in sibNd.ChildNodes)
                        {
                            if (sibChild.Name == "radius" || sibChild.Name == "aftradius")
                            {
                                found = true;
                                radius = Double.Parse(sibChild.InnerText);
                            }
                        }
                        if (found)
                            break;
                    }
                }
                else
                    radius = Double.Parse(nd.InnerText);
            }
        }

        Brep brep = null;
        double innerRadius = radius - thickness;
        Plane planeCyl = new Plane(new Point3d(0, 0, 0), Vector3d.XAxis);
        Circle innerCir = new Circle(planeCyl, innerRadius);
        Circle outerCir = new Circle(planeCyl, radius);

        Cylinder innerCyl = new Cylinder(innerCir, length);
        Cylinder outerCyl = new Cylinder(outerCir, length);

        Brep brepInner = Brep.CreateFromCylinder(innerCyl, true, true);
        Brep brepOuter = Brep.CreateFromCylinder(outerCyl, true, true);

        Brep[] tube = Brep.CreateBooleanDifference(brepOuter, brepInner, 0.0001);
        brep = tube[0];

        Transform trans = Transform.Translation(new Vector3d(stackLength, 0, 0));
        brep.Transform(trans);

        if (doc.Objects.AddBrep(brep) != Guid.Empty)
        {
            doc.Views.Redraw();
            result = Rhino.Commands.Result.Success;
        }

        stackLength += length;

        foreach (XmlNode subNd in subCompNds)
        {
            if (subNd.Name == "bulkhead")
                result = CreateBulkhead(doc, subNd, radius, stackLength - length, stackLength);
            else if (subNd.Name == "tubecoupler")
                result = CreateTubeCoupler(doc, subNd, stackLength);
            else if (subNd.Name == "centeringring")
                result = CreateCenteringRing(doc, subNd, radius, stackLength - length, stackLength);
            else if (subNd.Name == "innertube")
                result = CreateInnerTube(doc, subNd, stackLength - length, stackLength);
            else if (subNd.Name == "freeformfinset")
                result = CreateFreeFormFin(doc, subNd, radius, stackLength - length, stackLength);
        }

        return result;
    }

    private Result CreateInnerTube(RhinoDoc doc, XmlNode compNd, double xStart, double xEnd)
    {
        Result result = Rhino.Commands.Result.Failure;
        XmlElement compEle = compNd as XmlElement;
        XmlNodeList subCompNds = compNd.SelectNodes("subcomponents/*");

        double length = 0;
        PositionType positionType = PositionType.Top;
        double position = 0;
        double radialPosition = 0;
        double radialDirection = 0;
        double thickness = 0;
        double outerRadius = 0;

        foreach (XmlNode nd in compEle.ChildNodes)
        {
            if (nd.Name == "length")
                length = Double.Parse(nd.InnerText);
            else if (nd.Name == "position")
            {
                position = Double.Parse(nd.InnerText);
                positionType = (PositionType)Enum.Parse(typeof(PositionType), ((XmlElement)nd).GetAttribute("type"), true);
            }
            else if (nd.Name == "radialposition")
                radialPosition = Double.Parse(nd.InnerText);
            else if (nd.Name == "radialdirection")
                radialDirection = Double.Parse(nd.InnerText);
            else if (nd.Name == "outerradius")
                 outerRadius = Double.Parse(nd.InnerText);
            else if (nd.Name == "thickness")
                 thickness = Double.Parse(nd.InnerText);
           
        }

        Brep brep = null;

        double innerRadius = outerRadius - thickness;
        Plane planeCyl = new Plane(new Point3d(0, 0, 0), Vector3d.XAxis);
        Circle innerCir = new Circle(planeCyl, innerRadius);
        Circle outerCir = new Circle(planeCyl, outerRadius);
        Cylinder innerCyl = new Cylinder(innerCir, length);
        Cylinder outerCyl = new Cylinder(outerCir, length);
        
        Brep brepInner = Brep.CreateFromCylinder(innerCyl, true, true);
        Brep brepOuter = Brep.CreateFromCylinder(outerCyl, true, true);

        Brep[] tube = Brep.CreateBooleanDifference(brepOuter, brepInner, 0.0001);
        brep = tube[0];

        double xLoc = 0;
        switch (positionType)
        {
            case PositionType.Top:
                xLoc = xStart + position;
                break;
            case PositionType.Bottom:
                xLoc = xEnd - length + position;
                break;
            case PositionType.Middle:
                xLoc = (xEnd - xStart) / 2 + position;
                break;
            case PositionType.After:
                xLoc = xEnd - length + position;
                break;
            case PositionType.Absolute:
                xLoc = position;
                break;
        }

        Transform trans = Transform.Translation(new Vector3d(xLoc, 0, 0));
        brep.Transform(trans);

        if (doc.Objects.AddBrep(brep) != Guid.Empty)
        {
            doc.Views.Redraw();
            result = Rhino.Commands.Result.Success;
        }

        return result;
    }
    private Result CreateTubeCoupler(RhinoDoc doc, XmlNode compNd, double stackLength)
    {
        Result result = Rhino.Commands.Result.Failure;
        XmlElement compEle = compNd as XmlElement;
        XmlNodeList subCompNds = compNd.SelectNodes("subcomponents/*");

        double length = 0;
        double thickness = 0;
        double position = 0;
        PositionType positionType = PositionType.Top;
        double radialPosition = 0;
        double radialDirection = 0;
        double outerRadius = 0;
       
        foreach (XmlNode nd in compEle.ChildNodes)
        {
            if (nd.Name == "length")
                length = Double.Parse(nd.InnerText);
            else if (nd.Name == "thickness")
                thickness = Double.Parse(nd.InnerText);
            else if (nd.Name == "position")
            {
                position = Double.Parse(nd.InnerText);
                positionType = (PositionType)Enum.Parse(typeof(PositionType), ((XmlElement)nd).GetAttribute("type"), true);
            }
            else if (nd.Name == "radialposition")
                radialPosition = Double.Parse(nd.InnerText);
            else if (nd.Name == "radialdirection")
                radialDirection = Double.Parse(nd.InnerText);
            else if (nd.Name == "outerradius")
                outerRadius = Double.Parse(nd.InnerText);
          
        }

        Brep brep = null;

        double innerRadius = outerRadius - thickness;

        Plane planeCyl = new Plane(new Point3d(0, 0, 0), Vector3d.XAxis);
        Circle innerCir = new Circle(planeCyl, innerRadius);
        Circle outerCir = new Circle(planeCyl, outerRadius);

        Cylinder innerCyl = new Cylinder(innerCir, length);
        Cylinder outerCyl = new Cylinder(outerCir, length);

        Brep brepInner = Brep.CreateFromCylinder(innerCyl, true, true);
        Brep brepOuter = Brep.CreateFromCylinder(outerCyl, true, true);

        Brep[] tube = Brep.CreateBooleanDifference(brepOuter, brepInner, 0.0001);
        
        brep = tube[0];
        double xLoc = 0;
        switch (positionType)
        { 
            case PositionType.Top:
                xLoc = 0 + length + position;
                break;
            case PositionType.Bottom:
                xLoc = stackLength - length + position;
                break;
            case PositionType.Middle:
                xLoc = stackLength - length/2 + position;
                break;
            case PositionType.After:
                xLoc = stackLength + length + position;
                break;
            case PositionType.Absolute:
                xLoc = position;
                break;
        }
        Transform trans = Transform.Translation(new Vector3d(xLoc, 0, 0));
        brep.Transform(trans);

        if (doc.Objects.AddBrep(brep) != Guid.Empty)
        {
            doc.Views.Redraw();
            result = Rhino.Commands.Result.Success;
        }

        foreach (XmlNode subNd in subCompNds)
        {
            if (subNd.Name == "bulkhead")
                result = CreateBulkhead(doc, subNd, outerRadius, xLoc, xLoc+length);
        }

        return result;
    }

    private Result CreateBulkhead(RhinoDoc doc, XmlNode compNd, double parentRadius, double xStart, double xEnd)
    {
        Result result = Rhino.Commands.Result.Failure;
        XmlElement compEle = compNd as XmlElement;
        XmlNodeList subCompNds = compNd.SelectNodes("subcomponents/*");

        double length = 0;
        PositionType positionType = PositionType.Top;
        double position = 0;
        double radialPosition = 0;
        double radialDirection = 0;
        double outerRadius = 0;

        foreach (XmlNode nd in compEle.ChildNodes)
        {
            if (nd.Name == "length")
                length = Double.Parse(nd.InnerText);
            else if (nd.Name == "position")
            {
                position = Double.Parse(nd.InnerText);
                positionType = (PositionType)Enum.Parse(typeof(PositionType), ((XmlElement)nd).GetAttribute("type"), true);
            }
            else if (nd.Name == "radialposition")
                radialPosition = Double.Parse(nd.InnerText);
            else if (nd.Name == "radialdirection")
                radialDirection = Double.Parse(nd.InnerText);
            else if (nd.Name == "outerradius")
            {
                if (nd.InnerText == "auto")
                    outerRadius = parentRadius;
                else
                    outerRadius = Double.Parse(nd.InnerText);
            }

        }

        Brep brep = null;
        
        Plane planeCyl = new Plane(new Point3d(0, 0, 0), Vector3d.XAxis);
        Circle outerCir = new Circle(planeCyl, outerRadius);
        Cylinder outerCyl = new Cylinder(outerCir, length);
        brep = Brep.CreateFromCylinder(outerCyl, true, true);

        double xLoc = 0;
        switch (positionType)
        {
            case PositionType.Top:
                xLoc = xStart + position;
                break;
            case PositionType.Bottom:
                xLoc = xEnd - length + position;
                break;
            case PositionType.Middle:
                xLoc = (xEnd - xStart) / 2 + position;
                break;
            case PositionType.After:
                xLoc = xEnd - length + position;
                break;
            case PositionType.Absolute:
                xLoc = position;
                break;
        }
        Transform trans = Transform.Translation(new Vector3d(xLoc, 0, 0));
        brep.Transform(trans);

        if (doc.Objects.AddBrep(brep) != Guid.Empty)
        {
            doc.Views.Redraw();
            result = Rhino.Commands.Result.Success;
        }
        
        return result;
    }

    private Result CreateCenteringRing(RhinoDoc doc, XmlNode compNd, double parentRadius, double xStart, double xEnd)
    {
        Result result = Rhino.Commands.Result.Failure;
        XmlElement compEle = compNd as XmlElement;
        XmlNodeList subCompNds = compNd.SelectNodes("subcomponents/*");

        double length = 0;
        PositionType positionType = PositionType.Top;
        double position = 0;
        double radialPosition = 0;
        double radialDirection = 0;
        double innerRadius = 0;
        double outerRadius = 0;

        foreach (XmlNode nd in compEle.ChildNodes)
        {
            if (nd.Name == "length")
                length = Double.Parse(nd.InnerText);
            else if (nd.Name == "position")
            {
                position = Double.Parse(nd.InnerText);
                positionType = (PositionType)Enum.Parse(typeof(PositionType), ((XmlElement)nd).GetAttribute("type"), true);
            }
            else if (nd.Name == "radialposition")
                radialPosition = Double.Parse(nd.InnerText);
            else if (nd.Name == "radialdirection")
                radialDirection = Double.Parse(nd.InnerText);
            else if (nd.Name == "outerradius")
            {
                if (nd.InnerText == "auto")
                    outerRadius = parentRadius;
                else
                    outerRadius = Double.Parse(nd.InnerText);
            }
            else if (nd.Name == "innerradius")
            {
                if (nd.InnerText == "auto")
                {
                    bool found = false;

                    foreach (XmlNode sibNd in compNd.ParentNode.ChildNodes)
                    {
                        if (sibNd.Name == "innertube")
                        {
                            foreach (XmlNode sibChild in sibNd.ChildNodes)
                            {
                                if (sibChild.Name == "outerradius")
                                {
                                    found = true;
                                    innerRadius = Double.Parse(sibChild.InnerText);
                                }
                            }
                            if (found)
                                break;
                        }
                    }
                }
                else
                    innerRadius = Double.Parse(nd.InnerText);
            }

        }

        Brep brep = null;

        Plane planeCyl = new Plane(new Point3d(0, 0, 0), Vector3d.XAxis);
        Circle innerCir = new Circle(planeCyl, innerRadius);
        Circle outerCir = new Circle(planeCyl, outerRadius);
        Cylinder innerCyl = new Cylinder(innerCir, length);
        Cylinder outerCyl = new Cylinder(outerCir, length);
        
        Brep brepInner = Brep.CreateFromCylinder(innerCyl, true, true);
        Brep brepOuter = Brep.CreateFromCylinder(outerCyl, true, true);

        Brep[] tube = Brep.CreateBooleanDifference(brepOuter, brepInner, 0.0001);
        brep = tube[0];

        double xLoc = 0;
        switch (positionType)
        {
            case PositionType.Top:
                xLoc = xStart + position;
                break;
            case PositionType.Bottom:
                xLoc = xEnd - length + position;
                break;
            case PositionType.Middle:
                xLoc = (xEnd - xStart) / 2 + position;
                break;
            case PositionType.After:
                xLoc = xEnd - length + position;
                break;
            case PositionType.Absolute:
                xLoc = position;
                break;
        }
        Transform trans = Transform.Translation(new Vector3d(xLoc, 0, 0));
        brep.Transform(trans);

        if (doc.Objects.AddBrep(brep) != Guid.Empty)
        {
            doc.Views.Redraw();
            result = Rhino.Commands.Result.Success;
        }

        return result;
    }
    private Result CreateNoseCone(RhinoDoc doc, XmlNode compNd, ref double stackLength)
    {
        Result result = Rhino.Commands.Result.Failure;
        XmlElement compEle = compNd as XmlElement;
        XmlNodeList subCompNds = compNd.SelectNodes("subcomponents/*");

        double length = 0;
        double thickness = 0;
        NoseConeShapeType shape = NoseConeShapeType.Ogive;
        double shapeParameter = 0;
        double aftRadius = 0;
        double aftShoulderRadius = 0;
        double aftShoulderLength = 0;
        double aftShoulderThickness = 0;
        bool aftShoulderCapped = false;
        
        foreach (XmlNode nd in compEle.ChildNodes)
        {
            if (nd.Name == "length")
                length = Double.Parse(nd.InnerText);
            else if (nd.Name == "thickness")
                thickness = Double.Parse(nd.InnerText);
            else if (nd.Name == "shape")
                shape = (NoseConeShapeType)Enum.Parse(typeof(NoseConeShapeType), nd.InnerText, true);
            else if (nd.Name == "shapeparameter")
                shapeParameter = Double.Parse(nd.InnerText);
            else if (nd.Name == "aftradius")
            {
                if (nd.InnerText == "auto")
                {
                    XmlNode sibNd = compNd.NextSibling;
                    foreach (XmlNode sibChild in sibNd.ChildNodes)
                    {
                        if (sibNd.Name == "bodytube" && sibChild.Name == "radius")
                            aftRadius = Double.Parse(sibChild.InnerText);
                    }
                }
                else
                    aftRadius = Double.Parse(nd.InnerText);
            }
            else if (nd.Name == "aftshoulderradius")
                aftShoulderRadius = Double.Parse(nd.InnerText);
            else if (nd.Name == "aftshoulderlength")
                aftShoulderLength = Double.Parse(nd.InnerText);
            else if (nd.Name == "aftshoulderthickness")
                aftShoulderThickness = Double.Parse(nd.InnerText);
            else if (nd.Name == "aftshouldercapped")
                aftShoulderCapped = Boolean.Parse(nd.InnerText);
        }

        // generate geometry and create solid.
        int numberDivisions = 100;
        OgiveCurve ogive = new OgiveCurve(aftRadius, length);
        double xa = ogive.SphericalCapApex(0);
        double delta = (length - xa) / (numberDivisions - 1);
        double x = xa;
        double y = 0;
        List<Point3d> points = new List<Point3d>();

        for (int i = 0; i < numberDivisions; i++)
        {
            double angle = (double)i * System.Math.PI / (double)numberDivisions;
            y = ogive.Evaluate(x);

            points.Add(new Point3d(x, y, 0));

            x += delta;
        }

        Polyline curve = new Polyline(points);

        NurbsCurve nbCurve = curve.ToNurbsCurve();
        Curve[] offsetsCurves = nbCurve.Offset(new Plane(new Point3d(0, 0, 0), Vector3d.XAxis, Vector3d.YAxis), thickness, 0.0001, CurveOffsetCornerStyle.None); 
        
        Plane plane = new Plane(new Point3d(0, 0, 0), Vector3d.XAxis, Vector3d.ZAxis);

        Curve[] splits = offsetsCurves[0].Split(new PlaneSurface(plane, new Interval(0, 100), new Interval(0, 100)), 0.0001);

        LineCurve line1 = new LineCurve(nbCurve.PointAtStart, splits[1].PointAtStart);
        LineCurve line2 = new LineCurve(nbCurve.PointAtEnd, splits[1].PointAtEnd);

        List<Curve> curves = new List<Curve>() { nbCurve, splits[1], line1, line2 };

        Curve[] joined = Curve.JoinCurves(curves);

        RevSurface revsrf = RevSurface.Create(joined[0], new Line(new Point3d(0, 0, 0), new Point3d(length, 0, 0)), 0, 2 * Math.PI);
        Brep brep = Brep.CreateFromRevSurface(revsrf, true, true);
        brep.Flip();

        if (aftShoulderLength >0)
        {
            double innerRadius = aftShoulderRadius-aftShoulderThickness;
            double outerRadius = aftShoulderRadius;
            Plane planeCyl = new Plane(new Point3d(0,0,0),Vector3d.XAxis);
            Circle innerCir = new Circle(planeCyl,innerRadius);
            Circle outerCir = new Circle(planeCyl,outerRadius);

            Cylinder innerCyl = new Cylinder(innerCir, aftShoulderThickness + aftShoulderLength);
            Cylinder outerCyl = new Cylinder(outerCir, aftShoulderThickness + aftShoulderLength);

            Brep brepInner = Brep.CreateFromCylinder(innerCyl, true, true);
            Brep brepOuter = Brep.CreateFromCylinder(outerCyl, true, true);

            Brep[] tube = Brep.CreateBooleanDifference(brepOuter, brepInner, 0.0001);
            Transform trans = Transform.Translation(new Vector3d(length-aftShoulderThickness,0,0));

            tube[0].Transform(trans);

            Brep[] withShoulder = Brep.CreateBooleanUnion(new List<Brep>() { brep, tube[0] }, 0.001);
            brep = withShoulder[0];
        }


        if (doc.Objects.AddBrep(brep) != Guid.Empty)
        {
            doc.Views.Redraw();
            result = Rhino.Commands.Result.Success;
        }

        stackLength = length; // cone seems to start a new stack

        foreach (XmlNode subNd in subCompNds)
        {
            if (subNd.Name == "tubecoupler")
                result = CreateTubeCoupler(doc, subNd, stackLength);
        }

        return result;
    }
  }
}
