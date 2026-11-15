using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PulseGlassWidget.Controls
{
    public class CircularGauge : Control
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(CircularGauge),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(CircularGauge),
                new FrameworkPropertyMetadata("CPU", OnVisualPropChanged));

        public static readonly DependencyProperty SubTextProperty =
            DependencyProperty.Register("SubText", typeof(string), typeof(CircularGauge),
                new FrameworkPropertyMetadata("--°C", OnVisualPropChanged));

        public static readonly DependencyProperty WarnThresholdProperty =
            DependencyProperty.Register("WarnThreshold", typeof(double), typeof(CircularGauge),
                new FrameworkPropertyMetadata(70.0, OnVisualPropChanged));

        public static readonly DependencyProperty CritThresholdProperty =
            DependencyProperty.Register("CritThreshold", typeof(double), typeof(CircularGauge),
                new FrameworkPropertyMetadata(88.0, OnVisualPropChanged));

        public static readonly DependencyProperty NormalColorProperty =
            DependencyProperty.Register("NormalColor", typeof(Color), typeof(CircularGauge),
                new FrameworkPropertyMetadata(Color.FromRgb(255, 168, 125), FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropChanged));

        public static readonly DependencyProperty WarnColorProperty =
            DependencyProperty.Register("WarnColor", typeof(Color), typeof(CircularGauge),
                new FrameworkPropertyMetadata(Color.FromRgb(255, 77, 109), FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropChanged));

        public Color NormalColor
        {
            get { return (Color)GetValue(NormalColorProperty); }
            set { SetValue(NormalColorProperty, value); }
        }

        public Color WarnColor
        {
            get { return (Color)GetValue(WarnColorProperty); }
            set { SetValue(WarnColorProperty, value); }
        }

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public string SubText
        {
            get { return (string)GetValue(SubTextProperty); }
            set { SetValue(SubTextProperty, value); }
        }

        public double WarnThreshold
        {
            get { return (double)GetValue(WarnThresholdProperty); }
            set { SetValue(WarnThresholdProperty, value); }
        }

        public double CritThreshold
        {
            get { return (double)GetValue(CritThresholdProperty); }
            set { SetValue(CritThresholdProperty, value); }
        }

        private static void OnVisualPropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CircularGauge)d).InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 10) return;

            double thickness = Math.Max(4.5, size * 0.085);
            double radius = (size - thickness) / 2.0;
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);

            // Track ring
            var trackPen = new Pen(new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), thickness);
            trackPen.Freeze();
            dc.DrawEllipse(null, trackPen, center, radius, radius);

            // Value arc
            double pct = Math.Max(0, Math.Min(100, Value));
            Color arcColor = GetThresholdColor(pct);

            if (pct > 0.5)
            {
                double angle = (pct / 100.0) * 359.9;
                double startRad = -Math.PI / 2.0;
                double endRad = startRad + (angle * Math.PI / 180.0);

                Point startPt = new Point(
                    center.X + radius * Math.Cos(startRad),
                    center.Y + radius * Math.Sin(startRad));
                Point endPt = new Point(
                    center.X + radius * Math.Cos(endRad),
                    center.Y + radius * Math.Sin(endRad));

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(startPt, false, false);
                    ctx.ArcTo(endPt, new Size(radius, radius), 0, angle > 180, SweepDirection.Clockwise, true, false);
                }
                geo.Freeze();

                var arcPen = new Pen(new SolidColorBrush(arcColor), thickness)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                arcPen.Freeze();
                dc.DrawGeometry(null, arcPen, geo);
            }

            // Center text: Percentage
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var pctText = new FormattedText(
                string.Format("{0:0}%", pct),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                size * 0.21,
                Brushes.White,
                dpi);
            dc.DrawText(pctText, new Point(center.X - pctText.Width / 2.0, center.Y - pctText.Height * 0.72));

            // SubText (Temperature or GB)
            if (!string.IsNullOrEmpty(SubText))
            {
                var subFormatted = new FormattedText(
                    SubText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                    size * 0.15,
                    new SolidColorBrush(NormalColor),
                    dpi);
                dc.DrawText(subFormatted, new Point(center.X - subFormatted.Width / 2.0, center.Y + pctText.Height * 0.10));
            }
        }

        private Color GetThresholdColor(double pct)
        {
            if (pct >= CritThreshold) return Color.FromRgb(239, 68, 68);   // Kritik Kırmızı
            if (pct >= WarnThreshold) return WarnColor;                    // Ana Vurgu
            return NormalColor;                                            // İkincil Vurgu
        }
    }
}
