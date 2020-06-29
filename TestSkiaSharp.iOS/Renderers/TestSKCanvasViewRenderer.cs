using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Foundation;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using TestSkiaSharp;
using TestSkiaSharp.iOS.Renderers;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

[assembly: ExportRenderer(typeof(TestSKCanvasView), typeof(TestSKCanvasViewRenderer))]
namespace TestSkiaSharp.iOS.Renderers
{
    public class TestSKCanvasViewRenderer : TestSKCanvasViewRendererBase<SKCanvasView, SkiaSharp.Views.iOS.SKCanvasView>
    {
        public TestSKCanvasViewRenderer()
        {
            this.SetDisablesUserInteraction(true);
        }

        protected override SkiaSharp.Views.iOS.SKCanvasView CreateNativeControl()
        {
            SkiaSharp.Views.iOS.SKCanvasView nativeControl = base.CreateNativeControl();
            nativeControl.Opaque = false;
            return nativeControl;
        }
    }

	/// <summary>
	/// https://github.com/mono/SkiaSharp/blob/master/source/SkiaSharp.Views.Forms/SkiaSharp.Views.Forms.iOS/SKTouchHandler.cs
	/// </summary>
    internal class SKTouchHandler : UIGestureRecognizer
	{
        private readonly HashSet<UITouch> _touches = new HashSet<UITouch>();

		private Action<SKTouchEventArgs> onTouchAction;
		private Func<double, double, SKPoint> scalePixels;

		public SKTouchHandler(Action<SKTouchEventArgs> onTouchAction, Func<double, double, SKPoint> scalePixels)
		{
			this.onTouchAction = onTouchAction;
			this.scalePixels = scalePixels;

			DisablesUserInteraction = false;
		}

		public bool DisablesUserInteraction { get; set; }

		public void SetEnabled(UIView view, bool enableTouchEvents)
		{
			if (view != null)
			{
				if (!view.UserInteractionEnabled || DisablesUserInteraction)
				{
					view.UserInteractionEnabled = enableTouchEvents;
				}
				if (enableTouchEvents && view.GestureRecognizers?.Contains(this) != true)
				{
					view.AddGestureRecognizer(this);
				}
				else if (!enableTouchEvents && view.GestureRecognizers?.Contains(this) == true)
				{
					view.RemoveGestureRecognizer(this);
				}
			}
		}

		public void Detach(UIView view)
		{
			// clean the view
			SetEnabled(view, false);

			// remove references
			onTouchAction = null;
			scalePixels = null;
		}

		public override void TouchesBegan(NSSet touches, UIEvent evt)
		{
			base.TouchesBegan(touches, evt);

			foreach (UITouch touch in touches.Cast<UITouch>())
			{
				if (FireEvent(SKTouchAction.Pressed, touch, true))
                {
                    _touches.Add(touch);
                }
                else
                {
                    IgnoreTouch(touch, evt);
                }
			}
		}

		public override void TouchesMoved(NSSet touches, UIEvent evt)
		{
			base.TouchesMoved(touches, evt);

			foreach (UITouch touch in touches.Cast<UITouch>())
			{
				FireEvent(SKTouchAction.Moved, touch, true);
			}
		}

		public override void TouchesEnded(NSSet touches, UIEvent evt)
		{
			base.TouchesEnded(touches, evt);

			foreach (UITouch touch in touches.Cast<UITouch>())
			{
				FireEvent(SKTouchAction.Released, touch, false);
                _touches.Remove(touch);
            }
		}

		public override void TouchesCancelled(NSSet touches, UIEvent evt)
		{
			base.TouchesCancelled(touches, evt);

			foreach (UITouch touch in touches.Cast<UITouch>())
			{
				FireEvent(SKTouchAction.Cancelled, touch, false);
                _touches.Remove(touch);
			}
		}

        public override void Reset()
        {
            base.Reset();

            foreach (var touch in _touches)
            {
                FireEvent(SKTouchAction.Cancelled, touch, false);
            }

            _touches.Clear();
        }

        private bool FireEvent(SKTouchAction actionType, UITouch touch, bool inContact)
		{
			if (onTouchAction == null || scalePixels == null)
				return false;

			var id = touch.Handle.ToInt64();

			var cgPoint = touch.LocationInView(View);
			var point = scalePixels(cgPoint.X, cgPoint.Y);

			var args = new SKTouchEventArgs(id, actionType, point, inContact);
			onTouchAction(args);
			return args.Handled;
		}
	}

    public abstract class TestSKCanvasViewRendererBase<TFormsView, TNativeView> : ViewRenderer<TFormsView, TNativeView>
        where TFormsView : SKCanvasView
        where TNativeView : SkiaSharp.Views.iOS.SKCanvasView
    {
        private SKTouchHandler touchHandler;

        protected TestSKCanvasViewRendererBase()
        {
            this.Initialize();
        }

        private void Initialize()
        {
            this.touchHandler = new SKTouchHandler(
                (Action<SKTouchEventArgs>) (args => ((ISKCanvasViewController) this.Element).OnTouch(args)),
                (Func<double, double, SKPoint>) ((x, y) => this.GetScaledCoord(x, y)));
        }

        protected void SetDisablesUserInteraction(bool disablesUserInteraction)
        {
            this.touchHandler.DisablesUserInteraction = disablesUserInteraction;
        }

        protected override void OnElementChanged(ElementChangedEventArgs<TFormsView> e)
        {
            if ((object) e.OldElement != null)
            {
                ISKCanvasViewController oldElement = (ISKCanvasViewController) e.OldElement;
                oldElement.SurfaceInvalidated -= new EventHandler(this.OnSurfaceInvalidated);
                oldElement.GetCanvasSize -=
                    new EventHandler<GetPropertyValueEventArgs<SKSize>>(this.OnGetCanvasSize);
            }

            if ((object) e.NewElement != null)
            {
                ISKCanvasViewController newElement = (ISKCanvasViewController) e.NewElement;
                if ((object) this.Control == null)
                {
                    TNativeView nativeControl = this.CreateNativeControl();
                    nativeControl.PaintSurface +=
                        new EventHandler<SkiaSharp.Views.iOS.SKPaintSurfaceEventArgs>(this.OnPaintSurface);
                    this.SetNativeControl(nativeControl);
                }

                this.touchHandler.SetEnabled((UIView) this.Control, e.NewElement.EnableTouchEvents);
                this.Control.IgnorePixelScaling = e.NewElement.IgnorePixelScaling;
                newElement.SurfaceInvalidated += new EventHandler(this.OnSurfaceInvalidated);
                newElement.GetCanvasSize +=
                    new EventHandler<GetPropertyValueEventArgs<SKSize>>(this.OnGetCanvasSize);
                this.OnSurfaceInvalidated((object) newElement, EventArgs.Empty);
            }

            base.OnElementChanged(e);
        }

        protected override TNativeView CreateNativeControl()
        {
            return (TNativeView) Activator.CreateInstance(typeof(TNativeView));
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);
            if (e.PropertyName == SKCanvasView.IgnorePixelScalingProperty.PropertyName)
            {
                this.Control.IgnorePixelScaling = this.Element.IgnorePixelScaling;
            }
            else
            {
                if (!(e.PropertyName == SKCanvasView.EnableTouchEventsProperty.PropertyName))
                    return;
                this.touchHandler.SetEnabled((UIView) this.Control, this.Element.EnableTouchEvents);
            }
        }

        protected override void Dispose(bool disposing)
        {
            ISKCanvasViewController element = (ISKCanvasViewController) this.Element;
            if (element != null)
            {
                element.SurfaceInvalidated -= new EventHandler(this.OnSurfaceInvalidated);
                element.GetCanvasSize -=
                    new EventHandler<GetPropertyValueEventArgs<SKSize>>(this.OnGetCanvasSize);
            }

            TNativeView control = this.Control;
            if ((object) control != null)
                control.PaintSurface -=
                    new EventHandler<SkiaSharp.Views.iOS.SKPaintSurfaceEventArgs>(this.OnPaintSurface);
            this.touchHandler.Detach((UIView) control);
            base.Dispose(disposing);
        }

        private SKPoint GetScaledCoord(double x, double y)
        {
            if (!this.Element.IgnorePixelScaling)
            {
                x *= (double) this.Control.ContentScaleFactor;
                y *= (double) this.Control.ContentScaleFactor;
            }

            return new SKPoint((float) x, (float) y);
        }

        private void OnPaintSurface(object sender, SkiaSharp.Views.iOS.SKPaintSurfaceEventArgs e)
        {
            ((ISKCanvasViewController) this.Element)?.OnPaintSurface(
                new SKPaintSurfaceEventArgs(e.Surface, e.Info));
        }

        private void OnSurfaceInvalidated(object sender, EventArgs eventArgs)
        {
            this.Control.SetNeedsDisplay();
        }

        private void OnGetCanvasSize(object sender, GetPropertyValueEventArgs<SKSize> e)
        {
            GetPropertyValueEventArgs<SKSize> propertyValueEventArgs = e;
            // ISSUE: variable of a boxed type
            TNativeView control = this.Control;
            SKSize skSize = control != null ? control.CanvasSize : SKSize.Empty;
            propertyValueEventArgs.Value = skSize;
        }
    }
}