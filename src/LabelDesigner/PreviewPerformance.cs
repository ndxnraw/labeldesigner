using System;using System.Diagnostics;
namespace EanEtikety {
// Numeric timings only; no printer names, document values or filesystem paths.
public sealed class PreviewPerformance {
 public readonly double CaptureMs,QueueMs,DiscoveryMs,SetupMs,InspectionMs,RenderMs,DeliveryMs,ApplyMs,TotalMs;
 public readonly bool ArtworkCacheHit;public readonly int DriverGeometryReads,CapabilityReads,RenderedPages;
 internal PreviewPerformance(PreviewTrace t){CaptureMs=t.Captured;QueueMs=t.Started-t.Captured;DiscoveryMs=t.Discovered-t.Started;SetupMs=t.Setup-t.Discovered;InspectionMs=t.Inspected-t.Setup;RenderMs=t.Rendered-t.Inspected;DeliveryMs=t.Delivered-t.Rendered;ApplyMs=t.Now-t.Delivered;TotalMs=t.Now;ArtworkCacheHit=t.ArtworkCacheHit;DriverGeometryReads=t.DriverGeometryReads;CapabilityReads=t.CapabilityReads;RenderedPages=t.RenderedPages;}
}
internal sealed class PreviewTrace {
 readonly Stopwatch clock=Stopwatch.StartNew();public double Captured,Started,Discovered,Setup,Inspected,Rendered,Delivered;
 public bool ArtworkCacheHit;public int DriverGeometryReads,CapabilityReads,RenderedPages;
 public double Now{get{return clock.Elapsed.TotalMilliseconds;}}
}
}
