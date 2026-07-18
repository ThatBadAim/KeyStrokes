using KeyStrokes.Helpers;
using KeyStrokes.Services;

namespace KeyStrokes.ViewModels;

/// <summary>Base for each navigable page. The shell polls <see cref="Refresh"/> while the page is visible.</summary>
public abstract class PageViewModel : ObservableObject
{
    protected readonly TrackingService Tracking;

    protected PageViewModel(TrackingService tracking) => Tracking = tracking;

    public abstract string Title { get; }
    public abstract string Subtitle { get; }
    public abstract string Glyph { get; }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    /// <summary>How often the shell should poll this page while it is visible (in 200ms ticks).</summary>
    public virtual int RefreshEveryTicks => 1;

    /// <summary>Called once each time the page becomes the active view.</summary>
    public virtual void OnActivated() => Refresh();

    /// <summary>Pull fresh values from the tracking engine.</summary>
    public virtual void Refresh() { }
}
