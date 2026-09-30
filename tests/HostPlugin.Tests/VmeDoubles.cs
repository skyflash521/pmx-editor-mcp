namespace PmxEditorMcp.Tests
{
    public sealed class FakeVme : PEPlugin.Vme.IPEVme
    {
        public readonly System.Collections.Generic.List<PEPlugin.Vme.IPEVmeBone> Bones =
            new System.Collections.Generic.List<PEPlugin.Vme.IPEVmeBone>();

        public readonly System.Collections.Generic.List<PEPlugin.Vme.IPEVmeSingleValueElement> Morphs =
            new System.Collections.Generic.List<PEPlugin.Vme.IPEVmeSingleValueElement>();

        public FakeVme Named(string[] bones, string[] morphs)
        {
            foreach (string name in bones ?? new string[0])
            {
                Bones.Add(new FakeVmeBone { Name = name });
            }

            foreach (string name in morphs ?? new string[0])
            {
                Morphs.Add(new FakeVmeMorph { Name = name });
            }

            return this;
        }

        PEPlugin.Vme.IPEVmeBone PEPlugin.Vme.IPEVme.AddBone(System.String name, SlimDX.Vector3 pos) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVme.RemoveBone(PEPlugin.Vme.IPEVmeBone bone) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeSingleValueElement PEPlugin.Vme.IPEVme.AddMorph(System.String name) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVme.RemoveMorph(PEPlugin.Vme.IPEVmeSingleValueElement morph) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVme.Init(PEPlugin.Pmd.IPEPmd pmd) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVme.InitFromPmx(PEPlugin.Pmx.IPXPmx pmx) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVme.Clear() { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeGroupBone PEPlugin.Vme.IPEVme.CreateGroupBone(System.Boolean all) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeGroupBone PEPlugin.Vme.IPEVme.CreateGroupBone(System.Int32[] index) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeGroupMorph PEPlugin.Vme.IPEVme.CreateGroupMorph(System.Boolean all) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeGroupMorph PEPlugin.Vme.IPEVme.CreateGroupMorph(System.Int32[] index) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVme.SetVmd(PEPlugin.Vmd.IPEVmd vmd) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVme.SetVmeResult(PEPlugin.Vme.IPEVmeResult result) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeResult PEPlugin.Vme.IPEVme.Run(System.Int32 begin, System.Int32 end) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeResult PEPlugin.Vme.IPEVme.Run() { throw new System.NotSupportedException(); }
        System.Boolean PEPlugin.Vme.IPEVme.Preview(PEPlugin.Pmd.IPEPmd pmd, PEPlugin.Vme.PEVmePreviewOption option) { throw new System.NotSupportedException(); }
        System.Boolean PEPlugin.Vme.IPEVme.Preview(PEPlugin.Pmx.IPXPmx pmx, PEPlugin.Vme.PEVmePreviewOption option) { throw new System.NotSupportedException(); }
        System.Boolean PEPlugin.Vme.IPEVme.Preview(PEPlugin.Vme.PEVmePreviewOption option) { throw new System.NotSupportedException(); }
        PEPlugin.Pmd.IPEPmd PEPlugin.Vme.IPEVme.Pmd { get { throw new System.NotSupportedException(); } }
        System.Boolean PEPlugin.Vme.IPEVme.EnablePmd { get { throw new System.NotSupportedException(); } }
        PEPlugin.Pmx.IPXPmx PEPlugin.Vme.IPEVme.Pmx { get { throw new System.NotSupportedException(); } }
        System.Boolean PEPlugin.Vme.IPEVme.EnablePmx { get { throw new System.NotSupportedException(); } }
        System.Int32 PEPlugin.Vme.IPEVme.LastFrame { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeGroupBone PEPlugin.Vme.IPEVme.GroupBone { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeGroupMorph PEPlugin.Vme.IPEVme.GroupMorph { get { throw new System.NotSupportedException(); } }
        System.Collections.Generic.IList<PEPlugin.Vme.IPEVmeBone> PEPlugin.Vme.IPEVmeObject.Bone { get { return Bones; } }
        System.Collections.Generic.IList<PEPlugin.Vme.IPEVmeSingleValueElement> PEPlugin.Vme.IPEVmeObject.Morph { get { return Morphs; } }
        PEPlugin.Vme.IPEVmeCamera PEPlugin.Vme.IPEVmeObject.Camera { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeLight PEPlugin.Vme.IPEVmeObject.Light { get { throw new System.NotSupportedException(); } }
    }

    public sealed class FakeVmeResult : PEPlugin.Vme.IPEVmeResult
    {
        public int Frames;

        public bool BoneEnabled;

        public bool MorphEnabled;

        public int Bones;

        public int Morphs;

        System.String[] PEPlugin.Vme.IPEVmeResult.GetBoneNames() { throw new System.NotSupportedException(); }
        System.String[] PEPlugin.Vme.IPEVmeResult.GetMorphNames() { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeBoneResult PEPlugin.Vme.IPEVmeResult.GetBoneResult(System.Int32 frame, System.Int32 boneIndex) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeBoneResult PEPlugin.Vme.IPEVmeResult.Bone(System.Int32 frame, System.Int32 boneIndex) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeMorphResult PEPlugin.Vme.IPEVmeResult.GetMorphResult(System.Int32 frame, System.Int32 morphIndex) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeMorphResult PEPlugin.Vme.IPEVmeResult.Morph(System.Int32 frame, System.Int32 morphIndex) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeCameraResult PEPlugin.Vme.IPEVmeResult.GetCameraResult(System.Int32 frame) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeCameraResult PEPlugin.Vme.IPEVmeResult.Camera(System.Int32 frame) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeLightResult PEPlugin.Vme.IPEVmeResult.GetLightResult(System.Int32 frame) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeLightResult PEPlugin.Vme.IPEVmeResult.Light(System.Int32 frame) { throw new System.NotSupportedException(); }
        PEPlugin.Vmd.IPEVmd PEPlugin.Vme.IPEVmeResult.ToMotionVmd(System.Boolean preSkip) { throw new System.NotSupportedException(); }
        PEPlugin.Vmd.IPEVmd PEPlugin.Vme.IPEVmeResult.ToCameraVmd(System.Boolean preSkip, System.Boolean margin) { throw new System.NotSupportedException(); }
        PEPlugin.Vmd.IPEVmd PEPlugin.Vme.IPEVmeResult.ToLightVmd(System.Boolean preSkip) { throw new System.NotSupportedException(); }
        System.Boolean PEPlugin.Vme.IPEVmeResult.Preview(PEPlugin.Pmd.IPEPmd pmd, PEPlugin.Vme.PEVmePreviewOption option) { throw new System.NotSupportedException(); }
        System.Int32 PEPlugin.Vme.IPEVmeResult.StartFrame { get { throw new System.NotSupportedException(); } }
        int PEPlugin.Vme.IPEVmeResult.FrameCount { get { return Frames; } }
        bool PEPlugin.Vme.IPEVmeResult.EnableBone { get { return BoneEnabled; } }
        bool PEPlugin.Vme.IPEVmeResult.EnableMorph { get { return MorphEnabled; } }
        System.Boolean PEPlugin.Vme.IPEVmeResult.EnableCamera { get { throw new System.NotSupportedException(); } }
        System.Boolean PEPlugin.Vme.IPEVmeResult.EnableLight { get { throw new System.NotSupportedException(); } }
        int PEPlugin.Vme.IPEVmeResult.BoneCount { get { return Bones; } }
        int PEPlugin.Vme.IPEVmeResult.MorphCount { get { return Morphs; } }
    }

    public sealed class FakeVmeBone : PEPlugin.Vme.IPEVmeBone
    {
        public string Name;

        PEPlugin.Vme.IPEVmeBoneState PEPlugin.Vme.IPEVmeBone.ToBoneState(PEPlugin.Vme.IPEVmeEventState state) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeBoneState PEPlugin.Vme.IPEVmeBone.ToBS(PEPlugin.Vme.IPEVmeEventState state) { throw new System.NotSupportedException(); }
        System.Boolean PEPlugin.Vme.IPEVmeBone.IsTransformed { get { throw new System.NotSupportedException(); } }
        SlimDX.Vector3 PEPlugin.Vme.IPEVmeBone.InitialPosition { get { throw new System.NotSupportedException(); } set { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeBoneState PEPlugin.Vme.IPEVmeBone.BS { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeVectorValueEventOperator PEPlugin.Vme.IPEVmeBone.T { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeVectorValueEventOperator PEPlugin.Vme.IPEVmeBone.Translate { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeQuaternionValueEventOperator PEPlugin.Vme.IPEVmeBone.R { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeQuaternionValueEventOperator PEPlugin.Vme.IPEVmeBone.Rotate { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmePositionEventOperator PEPlugin.Vme.IPEVmeBone.P { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmePositionEventOperator PEPlugin.Vme.IPEVmeBone.Position { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeDirectionEventOperator PEPlugin.Vme.IPEVmeBone.D { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeDirectionEventOperator PEPlugin.Vme.IPEVmeBone.Direction { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeScalingEventOperator PEPlugin.Vme.IPEVmeBone.S { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeScalingEventOperator PEPlugin.Vme.IPEVmeBone.Scale { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeObject PEPlugin.Vme.IPEVmeElement.Extern { get { throw new System.NotSupportedException(); } }
        System.Boolean PEPlugin.Vme.IPEVmeElement.Enable { get { throw new System.NotSupportedException(); } set { throw new System.NotSupportedException(); } }
        string PEPlugin.Vme.IPEVmeElement.Name { get { return Name; } set { Name = value; } }
        void PEPlugin.Vme.IPEVmeFrameEvent.ClearEvents() { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.SetEvent(System.Int32 frame, PEPlugin.Vme.PEVmeEvent ev, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.SetEvent(System.Int32 frame, System.Action ev, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.SetEvent(System.Int32 frameSt, System.Int32 frameEd, PEPlugin.Vme.PEVmeEvent ev, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.SetEvent(System.Int32 frameSt, System.Int32 frameEd, System.Action ev, System.Int32 id) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.PEVmeEvent[] PEPlugin.Vme.IPEVmeFrameEvent.GetEvents(System.Int32 frame, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.RemoveEvent(PEPlugin.Vme.PEVmeEvent ev) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.RemoveEvent(System.Int32 frame, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.RunEvent(System.Int32 frame, System.Int32 id) { throw new System.NotSupportedException(); }
        System.Int32 PEPlugin.Vme.IPEVmeFrameEvent.EventCount { get { throw new System.NotSupportedException(); } }
        System.Int32 PEPlugin.Vme.IPEVmeFrameEvent.LastFrame { get { throw new System.NotSupportedException(); } }
        System.Boolean PEPlugin.Vme.IPEVmeFrameEvent.IsRunning { get { throw new System.NotSupportedException(); } }
    }

    public sealed class FakeVmeMorph : PEPlugin.Vme.IPEVmeSingleValueElement
    {
        public string Name;

        PEPlugin.Vme.IPEVmeSingleValueState PEPlugin.Vme.IPEVmeSingleValueElement.ToValueState(PEPlugin.Vme.IPEVmeEventState state) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeSingleValueState PEPlugin.Vme.IPEVmeSingleValueElement.ToVS(PEPlugin.Vme.IPEVmeEventState state) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.IPEVmeSingleValueState PEPlugin.Vme.IPEVmeSingleValueElement.VS { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeSingleValueEventOperator PEPlugin.Vme.IPEVmeSingleValueElement.V { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeSingleValueEventOperator PEPlugin.Vme.IPEVmeSingleValueElement.Value { get { throw new System.NotSupportedException(); } }
        PEPlugin.Vme.IPEVmeObject PEPlugin.Vme.IPEVmeElement.Extern { get { throw new System.NotSupportedException(); } }
        System.Boolean PEPlugin.Vme.IPEVmeElement.Enable { get { throw new System.NotSupportedException(); } set { throw new System.NotSupportedException(); } }
        string PEPlugin.Vme.IPEVmeElement.Name { get { return Name; } set { Name = value; } }
        void PEPlugin.Vme.IPEVmeFrameEvent.ClearEvents() { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.SetEvent(System.Int32 frame, PEPlugin.Vme.PEVmeEvent ev, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.SetEvent(System.Int32 frame, System.Action ev, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.SetEvent(System.Int32 frameSt, System.Int32 frameEd, PEPlugin.Vme.PEVmeEvent ev, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.SetEvent(System.Int32 frameSt, System.Int32 frameEd, System.Action ev, System.Int32 id) { throw new System.NotSupportedException(); }
        PEPlugin.Vme.PEVmeEvent[] PEPlugin.Vme.IPEVmeFrameEvent.GetEvents(System.Int32 frame, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.RemoveEvent(PEPlugin.Vme.PEVmeEvent ev) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.RemoveEvent(System.Int32 frame, System.Int32 id) { throw new System.NotSupportedException(); }
        void PEPlugin.Vme.IPEVmeFrameEvent.RunEvent(System.Int32 frame, System.Int32 id) { throw new System.NotSupportedException(); }
        System.Int32 PEPlugin.Vme.IPEVmeFrameEvent.EventCount { get { throw new System.NotSupportedException(); } }
        System.Int32 PEPlugin.Vme.IPEVmeFrameEvent.LastFrame { get { throw new System.NotSupportedException(); } }
        System.Boolean PEPlugin.Vme.IPEVmeFrameEvent.IsRunning { get { throw new System.NotSupportedException(); } }
    }
}
