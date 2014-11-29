﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logic.Core
{
    public class XWire : IShape
    {
        public IProperty[] Properties { get; set; }
        public IStyle Style { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public bool InvertStart { get; set; }
        public bool InvertEnd { get; set; }
        public XPin Start { get; set; }
        public XPin End { get; set; }

        public void Render(object dc, IRenderer renderer, IStyle style)
        {
            renderer.DrawWire(dc, style, this);
        }
    }
}
