// Decompiled with JetBrains decompiler
// Type: eyelogic.GazeSample
// Assembly: ELCsApi, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3C74D75E-7DC8-45E8-BA34-03EE7415E6FD
// Assembly location: D:\Develex\develex-bridge\DevelexBridge\Bridge\EyeTrackers\Libs\ELCsApi.dll

#nullable disable
namespace eyelogic
{
    public class FixationStartSample
    {
        public long timestampMicroSec;
        public int index;
        public Point2d por;
    }
    
    public class FixationEndSample
    {
        public long timestampMicroSec;
        public long timestampStartMicroSec;
        public int index;
        public int indexStart;
        public Point2d por;
    }
}