namespace PCDiag.Memory;

/// <summary>Abstraction over the pagefile providers (mock seam for tests).</summary>
public interface IPagefileSource
{
    PagefileInfo GetInfo();
}