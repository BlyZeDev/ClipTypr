namespace ClipTypr.NotifyIcon;

public sealed record MenuItem : IMenuItem
{
    public required string Text { get; set; }
    public bool? IsChecked { get; set; }
    public bool IsDisabled { get; set; }
    public EventHandler<NotifyIcon>? Click { get; set; }
    public IReadOnlyList<IMenuItem>? SubMenu { get; set; }
}