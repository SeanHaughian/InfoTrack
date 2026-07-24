namespace InfoTrack.Solicitors.Api.Models;

public sealed class LocationEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool Checked { get; set; }
}
