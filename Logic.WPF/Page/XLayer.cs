﻿using Logic.Core;
using Logic.Simulation;
using Logic.Util;
using Logic.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic.Page
{
    public class XLayer : ILayer
    {
        #region Constants

        public const string DefaultPageName = "Page";

        #endregion

        #region Enums

        public enum Mode
        {
            None,
            Selection,
            Create,
            Move
        }

        public enum Element
        {
            None,
            Selected,
            Line,
            Ellipse,
            Rectangle,
            Text,
            Wire,
            Pin,
            Block
        }

        #endregion

        #region Properties

        public XLayers Layers { get; set; }
        public IList<IShape> Shapes { get; set; }
        public ICollection<IShape> Hidden { get; set; }
        public IDictionary<XBlock, BoolSimulation> Simulations { get; set; }
        public ToolMenuModel Tool { get; set; }
        public bool EnableSnap { get; set; }
        public double SnapSize { get; set; }
        public IRenderer Renderer { get; set; }
        public History<XPage> History { get; set; }
        public ITextClipboard Clipboard { get; set; }
        public bool IsOverlay { get; set; }
        public Mode CurrentMode { get { return _mode; } }
        public bool SkipContextMenu { get; set; }
        public double RightX { get; set; }
        public double RightY { get; set; }
        public IStringSerializer Serializer { get; set; }
        public IStyle ShapeStyle { get; set; }
        public IStyle SelectedShapeStyle { get; set; }
        public IStyle SelectionStyle { get; set; }
        public IStyle HoverStyle { get; set; }
        public IStyle NullStateStyle { get; set; }
        public IStyle TrueStateStyle { get; set; }
        public IStyle FalseStateStyle { get; set; }
        public BoolSimulationCacheRenderer CacheRenderer { get; set; }
        public bool EnableSimulationCache { get; set; }

        #endregion

        #region Fields

        private double _startx, _starty;
        private double _hx, _hy;
        private XBlock _block = null;
        private XLine _line = null;
        private XEllipse _ellipse = null;
        private XRectangle _rectangle = null;
        private XText _text = null;
        private XWire _wire = null;
        private XPin _pin = null;
        private XRectangle _selection = null;
        private Mode _mode = Mode.None;
        private Element _element = Element.None;

        #endregion

        #region Constructor

        public XLayer()
        {
            Shapes = new List<IShape>();
            Hidden = new HashSet<IShape>();
            EnableSnap = true;
            SnapSize = 15.0;
        }

        #endregion

        #region History

        public void Snapshot()
        {
            if (History != null && Layers != null)
            {
                History.Snapshot(Layers.ToPage(DefaultPageName, null));
            }
        }

        #endregion

        #region ILayer

        public Func<bool> IsMouseCaptured { get; set; }
        public Action CaptureMouse { get; set; }
        public Action ReleaseMouseCapture { get; set; }
        public Action InvalidateVisual { get; set; }

        public void MouseLeftButtonDown(Point2 point)
        {
            switch (_mode)
            {
                case Mode.None:
                    if (IsMouseCaptured() == false)
                    {
                        if (Simulations != null && !IsOverlay)
                        {
                            // toggle block state in simulation mode
                            BlockToggleState(point);

                            // do not process other mouse events
                            return;
                        }

                        switch (Tool.CurrentTool)
                        {
                            case ToolMenuModel.Tool.None:
                                SelectionReset();
                                break;
                            case ToolMenuModel.Tool.Selection:
                                SelectionInit(point);
                                break;
                            case ToolMenuModel.Tool.Line:
                            case ToolMenuModel.Tool.Ellipse:
                            case ToolMenuModel.Tool.Rectangle:
                            case ToolMenuModel.Tool.Text:
                            case ToolMenuModel.Tool.Pin:
                                SelectionReset();
                                CreateInit(point);
                                break;
                            case ToolMenuModel.Tool.Wire:
                                SelectionReset();
                                CreateWireInit(point);
                                break;
                        }
                    }
                    break;
                case Mode.Selection:
                    break;
                case Mode.Create:
                    if (IsMouseCaptured())
                    {
                        switch (Tool.CurrentTool)
                        {
                            case ToolMenuModel.Tool.None:
                                break;
                            case ToolMenuModel.Tool.Line:
                            case ToolMenuModel.Tool.Ellipse:
                            case ToolMenuModel.Tool.Rectangle:
                            case ToolMenuModel.Tool.Text:
                            case ToolMenuModel.Tool.Pin:
                                CreateFinish(point);
                                break;
                            case ToolMenuModel.Tool.Wire:
                                CreateWireFinish(point);
                                break;
                        }
                    }
                    break;
                case Mode.Move:
                    break;
            }
        }

        public void MouseLeftButtonUp(Point2 point)
        {
            if (IsMouseCaptured())
            {
                switch (_mode)
                {
                    case Mode.None:
                        break;
                    case Mode.Selection:
                        SelectionFinish(point);
                        break;
                    case Mode.Create:
                        break;
                    case Mode.Move:
                        MoveFinish(point);
                        break;
                }
            }
        }

        public void MouseMove(Point2 point)
        {
            if (Layers != null
                && Simulations == null)
            {
                OverlayReset();
            }

            if (_mode != Mode.Move
                && _mode != Mode.Selection
                && Tool.CurrentTool != ToolMenuModel.Tool.None
                && Renderer != null
                && Renderer.Selected == null
                && Simulations == null)
            {
                OverlayMove(point);
            }

            if (IsMouseCaptured())
            {
                switch (_mode)
                {
                    case Mode.None:
                        break;
                    case Mode.Selection:
                        SelectionMove(point);
                        break;
                    case Mode.Create:
                        CreateMove(point);
                        break;
                    case Mode.Move:
                        Move(point);
                        break;
                };
            }
        }

        public void MouseRightButtonDown(Point2 point)
        {
            RightX = point.X;
            RightY = point.Y;

            if (IsMouseCaptured())
            {
                MouseCancel();
            }
        }

        public void MouseCancel()
        {
            if (IsMouseCaptured())
            {
                switch (_mode)
                {
                    case Mode.None:
                        break;
                    case Mode.Selection:
                        SelectionCancel();
                        SkipContextMenu = true;
                        break;
                    case Mode.Create:
                        CreateCancel();
                        SkipContextMenu = true;
                        break;
                    case Mode.Move:
                        MoveCancel();
                        SkipContextMenu = true;
                        break;
                }
            }
            else
            {
                Layers.Invalidate();
            }
        }

        public void OnRender(object dc)
        {
            if (Renderer != null)
            {
                var sw = Stopwatch.StartNew();

                if (_mode == Mode.Selection)
                {
                    _selection.Render(dc, Renderer, SelectionStyle);
                }
                else if (IsOverlay && Simulations != null)
                {
                    if (EnableSimulationCache)
                    {
                        if (CacheRenderer == null)
                        {
                            CacheRenderer = new BoolSimulationCacheRenderer()
                            {
                                Renderer = this.Renderer,
                                NullStateStyle = this.NullStateStyle,
                                TrueStateStyle = this.TrueStateStyle,
                                FalseStateStyle = this.FalseStateStyle,
                                Shapes = this.Shapes,
                                Simulations = this.Simulations
                            };
                        }
                        CacheRenderer.Render(dc);
                    }
                    else
                    {
                        RenderSimulationMode(dc);
                    }
                }
                else
                {
                    IStyle normal;
                    IStyle selected;
                    if (IsOverlay)
                    {
                        normal = HoverStyle;
                        selected = HoverStyle;
                    }
                    else
                    {
                        normal = ShapeStyle;
                        selected = SelectedShapeStyle;
                    }

                    if (Renderer != null
                        && Renderer.Selected != null)
                    {
                        RenderSelectedMode(dc, normal, selected);
                    }
                    else if (Renderer != null
                        && Renderer.Selected == null
                        && Hidden != null
                        && Hidden.Count > 0)
                    {
                        RenderHiddenMode(dc, normal);
                    }
                    else
                    {
                        RenderNormalMode(dc, normal);
                    }
                }

                sw.Stop();
                if (sw.Elapsed.TotalMilliseconds > (1000.0 / 60.0))
                {
                    Trace.TraceWarning("OnRender: " + sw.Elapsed.TotalMilliseconds + "ms");
                }
            }
        }

        #endregion

        #region Selection Mode

        public bool HaveSelected()
        {
            return Renderer != null
                && Renderer.Selected != null
                && Renderer.Selected.Count > 0;
        }

        public void SelectionDelete()
        {
            if (HaveSelected())
            {
                Snapshot();
                Layers.Delete(Renderer.Selected);
                SelectionReset();
            }
        }

        public void SelectionReset()
        {
            if (Renderer != null
                && Renderer.Selected != null)
            {
                Renderer.Selected = null;
                Layers.Invalidate();
            }
        }

        private void SelectionInit(Point2 p)
        {
            IShape shape = Layers != null ? Layers.HitTest(p) : null;
            if (shape != null)
            {
                MoveInit(shape, p);
            }
            else
            {
                SelectionReset();
                SelectionStart(p);
            }
        }

        private void SelectionStart(Point2 p)
        {
            _startx = p.X;
            _starty = p.Y;
            _selection = new XRectangle()
            {
                X = p.X,
                Y = p.Y,
                Width = 0.0,
                Height = 0.0,
                IsFilled = true
            };
            Shapes.Add(_selection);
            CaptureMouse();
            InvalidateVisual();
            _mode = Mode.Selection;
        }

        private void SelectionMove(Point2 p)
        {
            _selection.X = Math.Min(_startx, p.X);
            _selection.Y = Math.Min(_starty, p.Y);
            _selection.Width = Math.Abs(p.X - _startx);
            _selection.Height = Math.Abs(p.Y - _starty);
            InvalidateVisual();
        }

        private void SelectionFinish(Point2 p)
        {
            ReleaseMouseCapture();
            Shapes.Remove(_selection);
            InvalidateVisual();
            _mode = Mode.None;

            var rect = new Rect2(
                Math.Min(_startx, p.X),
                Math.Min(_starty, p.Y),
                Math.Abs(p.X - _startx),
                Math.Abs(p.Y - _starty));

            var hs = Layers.HitTest(rect);
            if (hs != null && hs.Count > 0)
            {
                Renderer.Selected = hs;
            }
            else
            {
                Renderer.Selected = null;
            }
            Layers.Invalidate();
        }

        private void SelectionCancel()
        {
            ReleaseMouseCapture();
            Shapes.Remove(_selection);
            InvalidateVisual();
            _mode = Mode.None;
        }

        #endregion

        #region Move Mode

        private void MoveInit(IShape shape, Point2 p)
        {
            if (History != null && Layers != null)
            {
                History.Hold(Layers.ToPage(DefaultPageName, null));
            }

            _startx = EnableSnap ? Snap(p.X, SnapSize) : p.X;
            _starty = EnableSnap ? Snap(p.Y, SnapSize) : p.Y;

            _hx = _startx;
            _hy = _starty;

            if (Renderer != null
                && Renderer.Selected != null)
            {
                _element = Element.Selected;
            }
            else
            {
                MoveInitElement(shape);
            }

            CaptureMouse();
            _mode = Mode.Move;
        }

        private void MoveInitElement(IShape shape)
        {
            if (shape is XLine)
            {
                _element = Element.Line;
                _line = shape as XLine;
            }
            else if (shape is XEllipse)
            {
                _element = Element.Ellipse;
                _ellipse = shape as XEllipse;
            }
            else if (shape is XRectangle)
            {
                _element = Element.Rectangle;
                _rectangle = shape as XRectangle;
            }
            else if (shape is XText)
            {
                _element = Element.Text;
                _text = shape as XText;
            }
            else if (shape is XWire)
            {
                _element = Element.Wire;
                _wire = shape as XWire;
            }
            else if (shape is XPin)
            {
                _element = Element.Pin;
                _pin = shape as XPin;
            }
            else if (shape is XBlock)
            {
                _element = Element.Block;
                _block = shape as XBlock;
            }
        }

        private void MoveFinish(Point2 p)
        {
            double x = EnableSnap ? Snap(p.X, SnapSize) : p.X;
            double y = EnableSnap ? Snap(p.Y, SnapSize) : p.Y;
            if (_hx != x || _hy != y)
            {
                if (History != null)
                {
                    History.Commit();
                }
            }
            else
            {
                if (History != null)
                {
                    History.Release();
                }
            }

            ReleaseMouseCapture();
            _mode = Mode.None;
        }

        private void MoveCancel()
        {
            if (History != null && Layers != null)
            {
                History.Release();
            }

            ReleaseMouseCapture();
            _mode = Mode.None;
        }

        public void Move(Point2 p)
        {
            double x = EnableSnap ? Snap(p.X, SnapSize) : p.X;
            double y = EnableSnap ? Snap(p.Y, SnapSize) : p.Y;

            double dx = x - _startx;
            double dy = y - _starty;

            _startx = x;
            _starty = y;

            if (Renderer != null
                && Renderer.Selected != null)
            {
                MoveSelected(Renderer.Selected, dx, dy);
            }
            else
            {
                MoveElement(dx, dy);
            }
        }

        public void Move(ICollection<IShape> shapes, double dx, double dy)
        {
            double x = EnableSnap ? Snap(dx, SnapSize) : dx;
            double y = EnableSnap ? Snap(dy, SnapSize) : dy;
            MoveSelected(shapes, x, y);
        }

        private void MoveElement(double dx, double dy)
        {
            switch (_element)
            {
                case Element.Line:
                    switch (Layers.LineHitResult)
                    {
                        case XLayers.LineHit.Start:
                            _line.X1 += dx;
                            _line.Y1 += dy;
                            break;
                        case XLayers.LineHit.End:
                            _line.X2 += dx;
                            _line.Y2 += dy;
                            break;
                        case XLayers.LineHit.Line:
                            _line.X1 += dx;
                            _line.Y1 += dy;
                            _line.X2 += dx;
                            _line.Y2 += dy;
                            break;
                    }
                    Layers.Shapes.InvalidateVisual();
                    break;
                case Element.Ellipse:
                    _ellipse.X += dx;
                    _ellipse.Y += dy;
                    Layers.Shapes.InvalidateVisual();
                    break;
                case Element.Rectangle:
                    _rectangle.X += dx;
                    _rectangle.Y += dy;
                    Layers.Shapes.InvalidateVisual();
                    break;
                case Element.Text:
                    _text.X += dx;
                    _text.Y += dy;
                    Layers.Shapes.InvalidateVisual();
                    break;
                case Element.Wire:
                    // TODO: Implement wire Move
                    break;
                case Element.Pin:
                    _pin.X += dx;
                    _pin.Y += dy;
                    Layers.Wires.InvalidateVisual();
                    Layers.Pins.InvalidateVisual();
                    break;
                case Element.Block:
                    Move(_block, dx, dy);
                    Layers.Blocks.InvalidateVisual();
                    Layers.Wires.InvalidateVisual();
                    Layers.Pins.InvalidateVisual();
                    break;
            }
        }

        public void MoveSelected(ICollection<IShape> shapes, double dx, double dy)
        {
            foreach (var shape in shapes)
            {
                if (shape is XLine)
                {
                    var line = shape as XLine;
                    line.X1 += dx;
                    line.Y1 += dy;
                    line.X2 += dx;
                    line.Y2 += dy;
                }
                else if (shape is XEllipse)
                {
                    var ellipse = shape as XEllipse;
                    ellipse.X += dx;
                    ellipse.Y += dy;
                }
                else if (shape is XRectangle)
                {
                    var rectangle = shape as XRectangle;
                    rectangle.X += dx;
                    rectangle.Y += dy;
                }
                else if (shape is XText)
                {
                    var text = shape as XText;
                    text.X += dx;
                    text.Y += dy;
                }
                else if (shape is XWire)
                {
                    var wire = shape as XWire;
                    // TODO: Implement wire Move
                }
                else if (shape is XPin)
                {
                    var pin = shape as XPin;
                    pin.X += dx;
                    pin.Y += dy;
                }
                else if (shape is XBlock)
                {
                    var block = shape as XBlock;
                    Move(block, dx, dy);
                }
            }

            Layers.Shapes.InvalidateVisual();
            Layers.Blocks.InvalidateVisual();
            Layers.Wires.InvalidateVisual();
            Layers.Pins.InvalidateVisual();
        }

        public void Move(XBlock block, double dx, double dy)
        {
            foreach (var shape in block.Shapes)
            {
                if (shape is XLine)
                {
                    var line = shape as XLine;
                    line.X1 += dx;
                    line.Y1 += dy;
                    line.X2 += dx;
                    line.Y2 += dy;
                }
                else if (shape is XEllipse)
                {
                    var ellipse = shape as XEllipse;
                    ellipse.X += dx;
                    ellipse.Y += dy;
                }
                else if (shape is XRectangle)
                {
                    var rectangle = shape as XRectangle;
                    rectangle.X += dx;
                    rectangle.Y += dy;
                }
                else if (shape is XText)
                {
                    var text = shape as XText;
                    text.X += dx;
                    text.Y += dy;
                }
                else if (shape is XWire)
                {
                    var wire = shape as XWire;
                    wire.X1 += dx;
                    wire.Y1 += dy;
                    wire.X2 += dx;
                    wire.Y2 += dy;
                }
                else if (shape is XPin)
                {
                    var pin = shape as XPin;
                    pin.X += dx;
                    pin.Y += dy;
                }
            }

            foreach (var pin in block.Pins)
            {
                pin.X += dx;
                pin.Y += dy;
            }
        }

        #endregion

        #region Overlay

        private void OverlayMove(Point2 p)
        {
            if (Layers == null)
                return;

            IShape shapeHitResult = null;
            IShape pinHitResult = null;
            IShape wireHitResult = null;
            IShape blockHitResult = null;

            shapeHitResult = Layers.HitTest(p);
            pinHitResult = Layers.HitTest(Layers.Pins.Shapes.Cast<XPin>(), p);
            if (pinHitResult == null)
            {
                wireHitResult = Layers.HitTest(Layers.Wires.Shapes.Cast<XWire>(), p);
                if (wireHitResult == null)
                {
                    blockHitResult = Layers.HitTest(Layers.Blocks.Shapes.Cast<XBlock>(), p);
                }
            }

            if (shapeHitResult != null
                && pinHitResult == null
                && wireHitResult == null
                && blockHitResult == null)
            {
                if (shapeHitResult is XBlock)
                {
                    XBlock block = shapeHitResult as XBlock;

                    Layers.Blocks.Hidden.Add(shapeHitResult);
                    Layers.Blocks.InvalidateVisual();

                    Layers.Overlay.Shapes.Add(block);
                    Layers.Overlay.InvalidateVisual();
                }
                else if (shapeHitResult is XPin)
                {
                    XPin pin = shapeHitResult as XPin;
                    if (Tool.CurrentTool == ToolMenuModel.Tool.Wire)
                    {
                        Layers.Blocks.Hidden.Add(pin);
                        Layers.Pins.Hidden.Add(pin);
                        Layers.Blocks.InvalidateVisual();
                        Layers.Pins.InvalidateVisual();

                        Layers.Overlay.Shapes.Add(pin);
                        Layers.Overlay.InvalidateVisual();
                    }
                }
                else if (shapeHitResult is XWire)
                {
                    if (Tool.CurrentTool == ToolMenuModel.Tool.Wire || Tool.CurrentTool == ToolMenuModel.Tool.Pin)
                    {
                        Layers.Wires.Hidden.Add(wireHitResult);
                        Layers.Wires.InvalidateVisual();

                        Layers.Overlay.Shapes.Add(wireHitResult);
                        Layers.Overlay.InvalidateVisual();
                    }
                }
                else
                {
                    if (Tool.CurrentTool == ToolMenuModel.Tool.Wire)
                    {
                        Layers.Shapes.Hidden.Add(shapeHitResult);
                        Layers.Shapes.InvalidateVisual();

                        Layers.Overlay.Shapes.Add(shapeHitResult);
                        Layers.Overlay.InvalidateVisual();
                    }
                }
            }

            if (pinHitResult != null)
            {
                XPin pin = pinHitResult as XPin;
                if (Tool.CurrentTool == ToolMenuModel.Tool.Wire)
                {
                    Layers.Pins.Hidden.Add(pin);
                    Layers.Blocks.Hidden.Add(pin);
                    Layers.Pins.InvalidateVisual();
                    Layers.Blocks.InvalidateVisual();

                    Layers.Overlay.Shapes.Add(pin);
                    Layers.Overlay.InvalidateVisual();
                }
            }
            else if (wireHitResult != null)
            {
                if (wireHitResult is XWire)
                {
                    if (Tool.CurrentTool == ToolMenuModel.Tool.Wire || Tool.CurrentTool == ToolMenuModel.Tool.Pin)
                    {
                        Layers.Wires.Hidden.Add(wireHitResult);
                        Layers.Wires.InvalidateVisual();

                        Layers.Overlay.Shapes.Add(wireHitResult);
                        Layers.Overlay.InvalidateVisual();
                    }
                }
                else if (wireHitResult is XPin)
                {
                    XPin pin = wireHitResult as XPin;
                    if (Tool.CurrentTool == ToolMenuModel.Tool.Wire)
                    {
                        if (pin.Owner == null)
                        {
                            Layers.Pins.Hidden.Add(pin);
                            Layers.Pins.InvalidateVisual();
                        }
                        else
                        {
                            Layers.Blocks.Hidden.Add(pin);
                            Layers.Blocks.InvalidateVisual();
                        }

                        Layers.Overlay.Shapes.Add(pin);
                        Layers.Overlay.InvalidateVisual();
                    }
                }
            }
            else if (blockHitResult != null)
            {
                if (blockHitResult is XBlock)
                {
                    XBlock block = shapeHitResult as XBlock;
                    if (Tool.CurrentTool == ToolMenuModel.Tool.Wire)
                    {
                        Layers.Blocks.Hidden.Add(block);
                        Layers.Blocks.InvalidateVisual();

                        Layers.Overlay.Shapes.Add(block);
                        Layers.Overlay.InvalidateVisual();
                    }
                }
                else if (blockHitResult is XPin)
                {
                    XPin pin = blockHitResult as XPin;
                    if (Tool.CurrentTool == ToolMenuModel.Tool.Wire)
                    {
                        Layers.Blocks.Hidden.Add(pin);
                        Layers.Blocks.InvalidateVisual();

                        Layers.Overlay.Shapes.Add(pin);
                        Layers.Overlay.InvalidateVisual();
                    }
                }
            }
        }

        private void OverlayReset()
        {
            if (Layers.Overlay.Shapes.Count > 0)
            {
                if (Layers.Shapes.Hidden != null && Layers.Shapes.Hidden.Count > 0)
                {
                    Layers.Shapes.Hidden.Clear();
                    Layers.Shapes.InvalidateVisual();
                }

                if (Layers.Blocks.Hidden != null && Layers.Blocks.Hidden.Count > 0)
                {
                    Layers.Blocks.Hidden.Clear();
                    Layers.Blocks.InvalidateVisual();
                }

                if (Layers.Wires.Hidden != null && Layers.Wires.Hidden.Count > 0)
                {
                    Layers.Wires.Hidden.Clear();
                    Layers.Wires.InvalidateVisual();
                }

                if (Layers.Pins.Hidden != null && Layers.Pins.Hidden.Count > 0)
                {
                    Layers.Pins.Hidden.Clear();
                    Layers.Pins.InvalidateVisual();
                }

                Layers.Pins.Hidden.Clear();
                Layers.Overlay.Shapes.Clear();
                Layers.Overlay.InvalidateVisual();
            }
        }

        #endregion

        #region Create Mode

        private void CreateWireInit(Point2 p)
        {
            IShape pinHitResult = null;
            IShape wireHitResult = null;
            IShape blockHitResult = null;

            if (Layers != null)
            {
                pinHitResult = Layers.HitTest(Layers.Pins.Shapes.Cast<XPin>(), p);
                if (pinHitResult == null)
                {
                    wireHitResult = Layers.HitTest(Layers.Wires.Shapes.Cast<XWire>(), p);
                    if (wireHitResult == null)
                    {
                        blockHitResult = Layers.HitTest(Layers.Blocks.Shapes.Cast<XBlock>(), p);
                    }
                }
            }

            CreateInit(p);

            double x = EnableSnap ? Snap(p.X, SnapSize) : p.X;
            double y = EnableSnap ? Snap(p.Y, SnapSize) : p.Y;

            if (pinHitResult == null
                && wireHitResult == null
                && (blockHitResult == null || (blockHitResult != null && !(blockHitResult is XPin))))
            {
                if (Layers.Pins != null)
                {
                    // create new standalone pin
                    pinHitResult = new XPin()
                    {
                        Name = "P",
                        PinType = PinType.Standalone,
                        Owner = null,
                        X = x,
                        Y = y
                    };

                    _pin = pinHitResult as XPin;

                    Shapes.Add(_pin);
                    InvalidateVisual();
                }
            }

            if (pinHitResult != null
                || wireHitResult != null
                || (blockHitResult != null && blockHitResult is XPin))
            {
                // connect wire start
                if (pinHitResult != null)
                {
                    _wire.Start = pinHitResult as XPin;
                }
                else if (wireHitResult != null && wireHitResult is XWire)
                {
                    // split wire
                    if (Layers.Pins != null && Layers.Wires != null)
                    {
                        WireSplitStart(wireHitResult, x, y);
                    }
                }
                else if (blockHitResult != null && blockHitResult is XPin)
                {
                    _wire.Start = blockHitResult as XPin;
                }
            }
        }

        private void CreateWireFinish(Point2 p)
        {
            IShape pinHitResult = null;
            IShape wireHitResult = null;
            IShape blockHitResult = null;

            if (Layers != null)
            {
                pinHitResult = Layers.HitTest(Layers.Pins.Shapes.Cast<XPin>(), p);
                if (pinHitResult == null)
                {
                    wireHitResult = Layers.HitTest(Layers.Wires.Shapes.Cast<XWire>(), p);
                    if (wireHitResult == null)
                    {
                        blockHitResult = Layers.HitTest(Layers.Blocks.Shapes.Cast<XBlock>(), p);
                    }
                }
            }

            CreateFinish(p);

            double x = EnableSnap ? Snap(p.X, SnapSize) : p.X;
            double y = EnableSnap ? Snap(p.Y, SnapSize) : p.Y;

            if (pinHitResult == null
                && wireHitResult == null
                && (blockHitResult == null || (blockHitResult != null && !(blockHitResult is XPin))))
            {
                if (Layers.Pins != null)
                {
                    // create new standalone pin
                    pinHitResult = new XPin()
                    {
                        Name = "P",
                        PinType = PinType.Standalone,
                        Owner = null,
                        X = x,
                        Y = y
                    };

                    Layers.Pins.Shapes.Add(pinHitResult);
                    Layers.Pins.InvalidateVisual();
                }
            }

            // connect wire end
            if (pinHitResult != null)
            {
                _wire.End = pinHitResult as XPin;
            }
            else if (wireHitResult != null && wireHitResult is XWire)
            {
                // split wire
                if (Layers.Pins != null && Layers.Wires != null)
                {
                    WireSplitEnd(wireHitResult, x, y);
                }
            }
            else if (blockHitResult != null && blockHitResult is XPin)
            {
                _wire.End = blockHitResult as XPin;
            }
        }

        private void CreateInit(Point2 p)
        {
            double x = EnableSnap ? Snap(p.X, SnapSize) : p.X;
            double y = EnableSnap ? Snap(p.Y, SnapSize) : p.Y;

            switch (Tool.CurrentTool)
            {
                case ToolMenuModel.Tool.Line:
                    {
                        _line = new XLine()
                        {
                            X1 = x,
                            Y1 = y,
                            X2 = x,
                            Y2 = y
                        };
                        Shapes.Add(_line);
                        CaptureMouse();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Ellipse:
                    {
                        _startx = x;
                        _starty = y;
                        _ellipse = new XEllipse()
                        {
                            X = x,
                            Y = y,
                            RadiusX = 0.0,
                            RadiusY = 0.0
                        };
                        Shapes.Add(_ellipse);
                        CaptureMouse();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Rectangle:
                    {
                        _startx = x;
                        _starty = y;
                        _rectangle = new XRectangle()
                        {
                            X = x,
                            Y = y,
                            Width = 0.0,
                            Height = 0.0
                        };
                        Shapes.Add(_rectangle);
                        CaptureMouse();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Text:
                    {
                        _startx = x;
                        _starty = y;
                        _text = new XText()
                        {
                            Text = "Text",
                            X = x,
                            Y = y,
                            Width = 0.0,
                            Height = 0.0,
                            IsFilled = false,
                            HAlignment = HAlignment.Center,
                            VAlignment = VAlignment.Center,
                            FontName = "Consolas",
                            FontSize = 12.0
                        };
                        Shapes.Add(_text);
                        CaptureMouse();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Wire:
                    {
                        _pin = null;
                        _wire = new XWire()
                        {
                            X1 = x,
                            Y1 = y,
                            X2 = x,
                            Y2 = y,
                            InvertStart = false,
                            InvertEnd = false
                        };
                        Shapes.Add(_wire);
                        CaptureMouse();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Pin:
                    {
                        // create new standalone pin
                        _pin = new XPin()
                        {
                            Name = "P",
                            PinType = PinType.Standalone,
                            Owner = null,
                            X = x,
                            Y = y
                        };
                        Shapes.Add(_pin);
                        CaptureMouse();
                        InvalidateVisual();
                    }
                    break;
            }
            _mode = Mode.Create;
        }

        private void CreateMove(Point2 p)
        {
            double x = EnableSnap ? Snap(p.X, SnapSize) : p.X;
            double y = EnableSnap ? Snap(p.Y, SnapSize) : p.Y;

            switch (Tool.CurrentTool)
            {
                case ToolMenuModel.Tool.Line:
                    {
                        _line.X2 = x;
                        _line.Y2 = y;
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Ellipse:
                    {
                        _ellipse.RadiusX = Math.Abs(x - _startx) / 2.0;
                        _ellipse.RadiusY = Math.Abs(y - _starty) / 2.0;
                        _ellipse.X = Math.Min(_startx, x) + _ellipse.RadiusX;
                        _ellipse.Y = Math.Min(_starty, y) + _ellipse.RadiusY;
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Rectangle:
                    {
                        _rectangle.X = Math.Min(_startx, x);
                        _rectangle.Y = Math.Min(_starty, y);
                        _rectangle.Width = Math.Abs(x - _startx);
                        _rectangle.Height = Math.Abs(y - _starty);
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Text:
                    {
                        _text.X = Math.Min(_startx, x);
                        _text.Y = Math.Min(_starty, y);
                        _text.Width = Math.Abs(x - _startx);
                        _text.Height = Math.Abs(y - _starty);
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Wire:
                    {
                        _wire.X2 = x;
                        _wire.Y2 = y;
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Pin:
                    {
                        _pin.X = x;
                        _pin.Y = y;
                        InvalidateVisual();
                    }
                    break;
            }
        }

        private void CreateFinish(Point2 p)
        {
            double x = EnableSnap ? Snap(p.X, SnapSize) : p.X;
            double y = EnableSnap ? Snap(p.Y, SnapSize) : p.Y;

            switch (Tool.CurrentTool)
            {
                case ToolMenuModel.Tool.Line:
                    {
                        _line.X2 = x;
                        _line.Y2 = y;
                        if (Layers.Shapes != null)
                        {
                            Shapes.Remove(_line);
                            Snapshot();
                            Layers.Shapes.Shapes.Add(_line);
                            Layers.Shapes.InvalidateVisual();
                        }
                        ReleaseMouseCapture();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Ellipse:
                    {
                        _ellipse.RadiusX = Math.Abs(x - _startx) / 2.0;
                        _ellipse.RadiusY = Math.Abs(y - _starty) / 2.0;
                        _ellipse.X = Math.Min(_startx, x) + _ellipse.RadiusX;
                        _ellipse.Y = Math.Min(_starty, y) + _ellipse.RadiusY;
                        if (Layers.Shapes != null)
                        {
                            Shapes.Remove(_ellipse);
                            Snapshot();
                            Layers.Shapes.Shapes.Add(_ellipse);
                            Layers.Shapes.InvalidateVisual();
                        }
                        ReleaseMouseCapture();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Rectangle:
                    {
                        _rectangle.X = Math.Min(_startx, x);
                        _rectangle.Y = Math.Min(_starty, y);
                        _rectangle.Width = Math.Abs(x - _startx);
                        _rectangle.Height = Math.Abs(y - _starty);
                        if (Layers.Shapes != null)
                        {
                            Shapes.Remove(_rectangle);
                            Snapshot();
                            Layers.Shapes.Shapes.Add(_rectangle);
                            Layers.Shapes.InvalidateVisual();
                        }
                        ReleaseMouseCapture();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Text:
                    {
                        _text.X = Math.Min(_startx, x);
                        _text.Y = Math.Min(_starty, y);
                        _text.Width = Math.Abs(x - _startx);
                        _text.Height = Math.Abs(y - _starty);
                        if (Layers.Shapes != null)
                        {
                            Shapes.Remove(_text);
                            Snapshot();
                            Layers.Shapes.Shapes.Add(_text);
                            Layers.Shapes.InvalidateVisual();
                        }
                        ReleaseMouseCapture();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Wire:
                    {
                        _wire.X2 = x;
                        _wire.Y2 = y;
                        if (Layers.Wires != null)
                        {
                            Shapes.Remove(_wire);

                            if (_pin != null)
                            {
                                Shapes.Remove(_pin);
                            }

                            Snapshot();

                            Layers.Wires.Shapes.Add(_wire);
                            Layers.Wires.InvalidateVisual();

                            if (_pin != null)
                            {
                                Layers.Pins.Shapes.Add(_pin);
                                Layers.Pins.InvalidateVisual();
                            }
                        }
                        ReleaseMouseCapture();
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Pin:
                    {
                        _pin.X = x;
                        _pin.Y = y;
                        if (Layers.Pins != null)
                        {
                            Shapes.Remove(_pin);
                            Snapshot();
                            Layers.Pins.Shapes.Add(_pin);
                            Layers.Pins.InvalidateVisual();
                        }
                        ReleaseMouseCapture();
                        InvalidateVisual();
                    }
                    break;
            }

            _mode = Mode.None;
        }

        private void CreateCancel()
        {
            switch (Tool.CurrentTool)
            {
                case ToolMenuModel.Tool.Line:
                    {
                        ReleaseMouseCapture();
                        Shapes.Remove(_line);
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Ellipse:
                    {
                        ReleaseMouseCapture();
                        Shapes.Remove(_ellipse);
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Rectangle:
                    {
                        ReleaseMouseCapture();
                        Shapes.Remove(_rectangle);
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Text:
                    {
                        ReleaseMouseCapture();
                        Shapes.Remove(_text);
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Wire:
                    {
                        ReleaseMouseCapture();
                        Shapes.Remove(_wire);
                        if (_pin != null)
                        {
                            Shapes.Remove(_pin);
                        }
                        InvalidateVisual();
                    }
                    break;
                case ToolMenuModel.Tool.Pin:
                    {
                        ReleaseMouseCapture();
                        Shapes.Remove(_pin);
                        InvalidateVisual();
                    }
                    break;
            }

            _mode = Mode.None;
        }

        #endregion

        #region Shape Properties

        public void ShapeToggleFill()
        {
            if (IsMouseCaptured() && Tool.CurrentTool == ToolMenuModel.Tool.Rectangle)
            {
                _rectangle.IsFilled = !_rectangle.IsFilled;
                InvalidateVisual();
            }
            else if (IsMouseCaptured() && Tool.CurrentTool == ToolMenuModel.Tool.Ellipse)
            {
                _ellipse.IsFilled = !_ellipse.IsFilled;
                InvalidateVisual();
            }
            else if (IsMouseCaptured() && Tool.CurrentTool == ToolMenuModel.Tool.Text)
            {
                _text.IsFilled = !_text.IsFilled;
                InvalidateVisual();
            }
            else
            {
                ShapeToggleSelectedFill();
            }
        }

        public void ShapeToggleInvertStart()
        {
            if (IsMouseCaptured() && Tool.CurrentTool == ToolMenuModel.Tool.Wire)
            {
                _wire.InvertStart = !_wire.InvertStart;
                InvalidateVisual();
            }
            else
            {
                ShapeToggleSelectedInvertStart();
            }
        }

        public void ShapeToggleInvertEnd()
        {
            if (IsMouseCaptured() && Tool.CurrentTool == ToolMenuModel.Tool.Wire)
            {
                _wire.InvertEnd = !_wire.InvertEnd;
                InvalidateVisual();
            }
            else
            {
                ShapeToggleSelectedInvertEnd();
            }
        }

        public void ShapeSetTextSizeDelta(double delta)
        {
            if (IsMouseCaptured() && Tool.CurrentTool == ToolMenuModel.Tool.Text)
            {
                double size = _text.FontSize + delta;
                if (size > 0.0)
                {
                    _text.FontSize = size;
                    InvalidateVisual();
                }
            }
            else
            {
                ShapeSetSelectedTextSizeDelta(delta);
            }
        }

        public void ShapeSetTextHAlignment(HAlignment halignment)
        {
            if (IsMouseCaptured() && Tool.CurrentTool == ToolMenuModel.Tool.Text)
            {
                _text.HAlignment = halignment;
                InvalidateVisual();
            }
            else
            {
                ShapeSetSelectedTextHAlignment(halignment);
            }
        }

        public void ShapeSetTextVAlignment(VAlignment valignment)
        {
            if (IsMouseCaptured() && Tool.CurrentTool == ToolMenuModel.Tool.Text)
            {
                _text.VAlignment = valignment;
                InvalidateVisual();
            }
            else
            {
                ShapeSetSelectedTextVAlignment(valignment);
            }
        }

        public void ShapeToggleSelectedFill()
        {
            if (HaveSelected())
            {
                var rectangles = Renderer.Selected.Where(x => x is XRectangle).Cast<XRectangle>();
                foreach (var rectangle in rectangles)
                {
                    rectangle.IsFilled = !rectangle.IsFilled;
                }

                var ellipses = Renderer.Selected.Where(x => x is XEllipse).Cast<XEllipse>();
                foreach (var ellipse in ellipses)
                {
                    ellipse.IsFilled = !ellipse.IsFilled;
                }

                var texts = Renderer.Selected.Where(x => x is XText).Cast<XText>();
                foreach (var text in texts)
                {
                    text.IsFilled = !text.IsFilled;
                }

                Layers.Shapes.InvalidateVisual();
            }
        }

        public void ShapeToggleSelectedInvertStart()
        {
            if (HaveSelected())
            {
                var wires = Renderer.Selected.Where(x => x is XWire).Cast<XWire>();
                foreach (var wire in wires)
                {
                    wire.InvertStart = !wire.InvertStart;
                }
                Layers.Wires.InvalidateVisual();
            }
        }

        public void ShapeToggleSelectedInvertEnd()
        {
            if (HaveSelected())
            {
                var wires = Renderer.Selected.Where(x => x is XWire).Cast<XWire>();
                foreach (var wire in wires)
                {
                    wire.InvertEnd = !wire.InvertEnd;
                }
                Layers.Wires.InvalidateVisual();
            }
        }

        public void ShapeSetSelectedTextSizeDelta(double delta)
        {
            if (HaveSelected())
            {
                var texts = Renderer.Selected.Where(x => x is XText).Cast<XText>();
                foreach (var text in texts)
                {
                    double size = text.FontSize + delta;
                    if (size > 0.0)
                    {
                        text.FontSize = size;
                    }
                }
                Layers.Shapes.InvalidateVisual();
            }
        }

        public void ShapeSetSelectedTextHAlignment(HAlignment halignment)
        {
            if (HaveSelected())
            {
                var texts = Renderer.Selected.Where(x => x is XText).Cast<XText>();
                foreach (var text in texts)
                {
                    text.HAlignment = halignment;
                }
                Layers.Shapes.InvalidateVisual();
            }
        }

        public void ShapeSetSelectedTextVAlignment(VAlignment valignment)
        {
            if (HaveSelected())
            {
                var texts = Renderer.Selected.Where(x => x is XText).Cast<XText>();
                foreach (var text in texts)
                {
                    text.VAlignment = valignment;
                }
                Layers.Shapes.InvalidateVisual();
            }
        }

        #endregion

        #region Snap

        public double Snap(double val, double snap)
        {
            double r = val % snap;
            return r >= snap / 2.0 ? val + snap - r : val - r;
        }

        #endregion

        #region Min

        public void Min(ICollection<IShape> shapes, ref double x, ref double y)
        {
            foreach (var shape in shapes)
            {
                if (shape is XLine)
                {
                    var line = shape as XLine;
                    x = Math.Min(x, line.X1);
                    y = Math.Min(y, line.Y1);
                    x = Math.Min(x, line.X2);
                    y = Math.Min(y, line.Y2);
                }
                else if (shape is XEllipse)
                {
                    var ellipse = shape as XEllipse;
                    x = Math.Min(x, ellipse.X);
                    y = Math.Min(y, ellipse.Y);
                }
                else if (shape is XRectangle)
                {
                    var rectangle = shape as XRectangle;
                    x = Math.Min(x, rectangle.X);
                    y = Math.Min(y, rectangle.Y);
                }
                else if (shape is XText)
                {
                    var text = shape as XText;
                    x = Math.Min(x, text.X);
                    y = Math.Min(y, text.Y);
                }
                else if (shape is XWire)
                {
                    var wire = shape as XWire;
                    if (wire.Start == null)
                    {
                        x = Math.Min(x, wire.X1);
                        y = Math.Min(y, wire.Y1);
                    }
                    if (wire.End == null)
                    {
                        x = Math.Min(x, wire.X2);
                        y = Math.Min(y, wire.Y2);
                    }
                }
                else if (shape is XPin)
                {
                    var pin = shape as XPin;
                    x = Math.Min(x, pin.X);
                    y = Math.Min(y, pin.Y);
                }
                else if (shape is XBlock)
                {
                    var block = shape as XBlock;
                    Min(block.Shapes, ref x, ref y);
                    foreach (var pin in block.Pins)
                    {
                        x = Math.Min(x, pin.X);
                        y = Math.Min(y, pin.Y);
                    }
                }
            }
        }

        #endregion

        #region Wire

        private void WireSplit(XWire wire, double x, double y, out XPin pin, out XWire split)
        {
            // create new standalone pin
            pin = new XPin()
            {
                Name = "P",
                PinType = PinType.Standalone,
                Owner = null,
                X = x,
                Y = y
            };

            split = new XWire()
            {
                Start = pin as XPin,
                End = wire.End,
                InvertStart = false,
                InvertEnd = wire.InvertEnd
            };

            wire.InvertEnd = false;
            wire.End = pin as XPin;
        }

        private void WireSplitStart(IShape wireHitResult, double x, double y)
        {
            XPin pin;
            XWire split;

            WireSplit(wireHitResult as XWire, x, y, out pin, out split);

            _wire.Start = pin;

            Layers.Pins.Shapes.Add(pin);
            Layers.Wires.Shapes.Add(split);

            Layers.Wires.InvalidateVisual();
            Layers.Pins.InvalidateVisual();
        }

        private void WireSplitEnd(IShape wireHitResult, double x, double y)
        {
            XPin pin;
            XWire split;

            WireSplit(wireHitResult as XWire, x, y, out pin, out split);

            _wire.End = pin;

            Layers.Pins.Shapes.Add(pin);
            Layers.Wires.Shapes.Add(split);

            Layers.Wires.InvalidateVisual();
            Layers.Pins.InvalidateVisual();
        }

        #endregion

        #region Block

        private XBlock Clone(XBlock source)
        {
            try
            {
                var block = new XBlock()
                {
                    Properties = source.Properties,
                    Database = source.Database,
                    Name = source.Name,
                    Style = source.Style,
                    Shapes = source.Shapes,
                    Pins = source.Pins
                };
                var json = Serializer.Serialize(block);
                var copy = Serializer.Deserialize<XBlock>(json);
                foreach (var pin in copy.Pins)
                {
                    pin.Owner = copy;
                }
                return copy;
            }
            catch (Exception ex)
            {
                Log.LogError("{0}{1}{2}",
                    ex.Message,
                    Environment.NewLine,
                    ex.StackTrace);
            }
            return null;
        }

        public XBlock Insert(XBlock block, double x, double y)
        {
            // clone block
            XBlock copy = Clone(block);

            if (copy != null)
            {
                // move to drop position
                double dx = EnableSnap ? Snap(x, SnapSize) : x;
                double dy = EnableSnap ? Snap(y, SnapSize) : y;
                Move(copy, dx, dy);

                // add to collection
                Layers.Blocks.Shapes.Add(copy);
                Layers.Blocks.InvalidateVisual();

                return copy;
            }

            return null;
        }

        private void Split(XWire wire, XPin pin0, XPin pin1)
        {
            // pins must be aligned horizontally or vertically
            if (pin0.X != pin1.X && pin0.Y != pin1.Y)
                return;

            // wire must be horizontal or vertical
            if (wire.Start.X != wire.End.X && wire.Start.Y != wire.End.Y)
                return;

            XWire split;
            if (wire.Start.X > wire.End.X || wire.Start.Y > wire.End.Y)
            {
                split = new XWire()
                {
                    Start = pin0,
                    End = wire.End,
                    InvertStart = false,
                    InvertEnd = wire.InvertEnd
                };
                wire.InvertEnd = false;
                wire.End = pin1;
            }
            else
            {
                split = new XWire()
                {
                    Start = pin1,
                    End = wire.End,
                    InvertStart = false,
                    InvertEnd = wire.InvertEnd
                };
                wire.InvertEnd = false;
                wire.End = pin0;
            }
            Layers.Wires.Shapes.Add(split);
            Layers.Pins.InvalidateVisual();
            Layers.Wires.InvalidateVisual();
        }

        public void Connect(XBlock block)
        {
            // check for pin to wire connections
            int count = block.Pins.Count();
            if (count > 0)
            {
                var wires = Layers.Wires.Shapes.Cast<XWire>();
                var dict = new Dictionary<XWire, List<XPin>>();

                // find connections
                foreach (var pin in block.Pins)
                {
                    IShape hit = Layers.HitTest(wires, new Point2(pin.X, pin.Y));
                    if (hit != null && hit is XWire)
                    {
                        var wire = hit as XWire;
                        if (dict.ContainsKey(wire))
                        {
                            dict[wire].Add(pin);
                        }
                        else
                        {
                            dict.Add(wire, new List<XPin>());
                            dict[wire].Add(pin);
                        }
                    }
                }

                // split wires
                foreach (var kv in dict)
                {
                    List<XPin> pins = kv.Value;
                    if (pins.Count == 2)
                    {
                        Split(kv.Key, pins[0], pins[1]);
                    }
                }
            }
        }

        #endregion

        #region Page

        public void New()
        {
            var page = new XPage()
            {
                Name = DefaultPageName,
                Shapes = new List<IShape>(),
                Blocks = new List<IShape>(),
                Pins = new List<IShape>(),
                Wires = new List<IShape>(),
                Template = null
            };
            Snapshot();
            Layers.Load(page);
            Layers.Invalidate();
        }

        public XPage Open(string path)
        {
            try
            {
                using (var fs = System.IO.File.OpenText(path))
                {
                    var json = fs.ReadToEnd();
                    var page = Serializer.Deserialize<XPage>(json);
                    return page;
                }
            }
            catch (Exception ex)
            {
                Log.LogError("{0}{1}{2}",
                    ex.Message,
                    Environment.NewLine,
                    ex.StackTrace);
            }
            return null;
        }

        public void Save(string path, XPage page)
        {
            try
            {
                var json = Serializer.Serialize(page);
                using (var fs = System.IO.File.CreateText(path))
                {
                    fs.Write(json);
                }
            }
            catch (Exception ex)
            {
                Log.LogError("{0}{1}{2}",
                    ex.Message,
                    Environment.NewLine,
                    ex.StackTrace);
            }
        }

        public void Load(string path)
        {
            var page = Open(path);
            if (page != null)
            {
                SelectionReset();
                Snapshot();
                Layers.Load(page);
                Layers.Invalidate();
            }
        }

        public void Save(string path)
        {
            Save(path, Layers.ToPage(DefaultPageName, null));
        }

        #endregion

        #region Clipboard

        private void CopyToClipboard(IList<IShape> shapes)
        {
            try
            {
                var json = Serializer.Serialize(shapes);
                if (!string.IsNullOrEmpty(json))
                {
                    Clipboard.SetText(json);
                }
            }
            catch (Exception ex)
            {
                Log.LogError("{0}{1}{2}",
                    ex.Message,
                    Environment.NewLine,
                    ex.StackTrace);
            }
        }

        #endregion

        #region Edit

        public void Undo()
        {
            if (History != null && Layers != null)
            {
                var page = History.Undo(Layers.ToPage(DefaultPageName, null));
                if (page != null)
                {
                    SelectionReset();
                    Layers.Load(page);
                    Layers.Invalidate();
                }
            }
        }

        public void Redo()
        {
            if (History != null && Layers != null)
            {
                var page = History.Redo(Layers.ToPage(DefaultPageName, null));
                if (page != null)
                {
                    SelectionReset();
                    Layers.Load(page);
                    Layers.Invalidate();
                }
            }
        }

        public bool CanCopy()
        {
            return HaveSelected();
        }

        public bool CanPaste()
        {
            try
            {
                return Clipboard.ContainsText();
            }
            catch (Exception ex)
            {
                Log.LogError("{0}{1}{2}",
                    ex.Message,
                    Environment.NewLine,
                    ex.StackTrace);
            }
            return false;
        }

        public void Cut()
        {
            if (CanCopy())
            {
                CopyToClipboard(Renderer.Selected.ToList());
                SelectionDelete();
            }
        }

        public void Copy()
        {
            if (CanCopy())
            {
                CopyToClipboard(Renderer.Selected.ToList());
            }
        }

        public void Paste()
        {
            try
            {
                if (CanPaste())
                {
                    var json = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(json))
                    {
                        Paste(json);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError("{0}{1}{2}",
                    ex.Message,
                    Environment.NewLine,
                    ex.StackTrace);
            }
        }

        public void Paste(string json)
        {
            try
            {
                var shapes = Serializer.Deserialize<IList<IShape>>(json);
                if (shapes != null && shapes.Count > 0)
                {
                    Paste(shapes);
                }
            }
            catch (Exception ex)
            {
                Log.LogError("{0}{1}{2}",
                    ex.Message,
                    Environment.NewLine,
                    ex.StackTrace);
            }
        }

        public void Paste(IEnumerable<IShape> shapes)
        {
            Snapshot();
            SelectionReset();
            Layers.Add(shapes);
            Renderer.Selected = new HashSet<IShape>(shapes);
            Layers.Invalidate();
        }

        #endregion

        #region Block

        public XBlock BlockCreateFromSelected(string name)
        {
            if (HaveSelected())
            {
                return BlockCreate(name, Renderer.Selected);
            }
            return null;
        }

        public XBlock BlockCreate(string name, IEnumerable<IShape> shapes)
        {
            var block = new XBlock()
            {
                Name = name,
                Shapes = new List<IShape>(),
                Pins = new List<XPin>()
            };

            foreach (var shape in shapes)
            {
                if (shape is XLine)
                {
                    block.Shapes.Add(shape);
                }
                else if (shape is XEllipse)
                {
                    block.Shapes.Add(shape);
                }
                else if (shape is XRectangle)
                {
                    block.Shapes.Add(shape);
                }
                else if (shape is XText)
                {
                    block.Shapes.Add(shape);
                }
                else if (shape is XWire)
                {
                    block.Shapes.Add(shape);
                }
                else if (shape is XPin)
                {
                    block.Pins.Add(shape as XPin);
                }
                else if (shape is XBlock)
                {
                    // Not supported.
                }
            }
            return block;
        }

        private void BlockToggleState(Point2 p)
        {
            IShape shape = Layers != null ? Layers.HitTest(p) : null;
            if (shape is XBlock)
            {
                var block = shape as XBlock;
                var simulation = Simulations[block];
                bool? state = simulation.State;
                simulation.State = !state;
            }
        }

        #endregion

        #region Render

        private void RenderSimulationMode(object dc)
        {
            foreach (var shape in Shapes)
            {
                if (shape is XBlock)
                {
                    var block = shape as XBlock;
                    bool? state = Simulations[block].State;
                    IStyle style;
                    switch (state)
                    {
                        case true:
                            style = TrueStateStyle;
                            break;
                        case false:
                            style = FalseStateStyle;
                            break;
                        case null:
                        default:
                            style = NullStateStyle;
                            break;
                    }
                    block.Render(dc, Renderer, style);
                    foreach (var pin in block.Pins)
                    {
                        pin.Render(dc, Renderer, style);
                    }
                }
            }
        }

        private void RenderNormalMode(object dc, IStyle style)
        {
            foreach (var shape in Shapes)
            {
                shape.Render(dc, Renderer, style);
                if (shape is XBlock)
                {
                    foreach (var pin in (shape as XBlock).Pins)
                    {
                        pin.Render(dc, Renderer, style);
                    }
                }
            }
        }

        private void RenderSelectedMode(object dc, IStyle normal, IStyle selected)
        {
            foreach (var shape in Shapes)
            {
                IStyle style = Renderer.Selected.Contains(shape) ? selected : normal;
                shape.Render(dc, Renderer, style);
                if (shape is XBlock)
                {
                    foreach (var pin in (shape as XBlock).Pins)
                    {
                        pin.Render(dc, Renderer, style);
                    }
                }
            }
        }

        private void RenderHiddenMode(object dc, IStyle style)
        {
            foreach (var shape in Shapes)
            {
                if (!Hidden.Contains(shape))
                {
                    shape.Render(dc, Renderer, style);
                    if (shape is XBlock)
                    {
                        foreach (var pin in (shape as XBlock).Pins)
                        {
                            if (!Hidden.Contains(pin))
                            {
                                pin.Render(dc, Renderer, style);
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}