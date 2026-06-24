// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// SmoothProgressBar.cs provides a small owner-drawn progress indicator. The
// native Windows progress bar can flicker or flash in remote backstage sessions,
// so this control paints a simple buffered track and fill instead.
// -----------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Windows.Forms;

namespace IPALot.Controls;

public sealed class SmoothProgressBar : Control
{
    private int _maximum = 100;
    private int _value;

    public SmoothProgressBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);

        Height = 22;
        BackColor = Color.FromArgb(229, 231, 235);
        ForeColor = Color.FromArgb(21, 128, 61);
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(1, value);
            _value = Math.Min(_value, _maximum);
            Invalidate();
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Max(0, Math.Min(value, Maximum));
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var trackBrush = new SolidBrush(BackColor);
        using var fillBrush = new SolidBrush(ForeColor);
        using var borderPen = new Pen(Color.FromArgb(188, 193, 201));

        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        e.Graphics.FillRectangle(trackBrush, bounds);

        var fillWidth = Maximum == 0 ? 0 : (int)Math.Round(bounds.Width * (Value / (double)Maximum));
        if (fillWidth > 0)
        {
            e.Graphics.FillRectangle(fillBrush, new Rectangle(0, 0, fillWidth, bounds.Height));
        }

        var borderBounds = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);
        e.Graphics.DrawRectangle(borderPen, borderBounds);
    }
}
