using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin;
using PEPlugin.Vme;
using PEPlugin.Vmd;
using SlimDX;

namespace PmxEditorMcp
{
    public static class MotionGetVmdCameraView
    {
        public const string ToolName = "motion_get_vmd_camera_view";

        public const string VmdName = "vmd";

        public const string FramesName = "frames";

        public const string CamerasName = "cameras";

        public const string FrameName = "frame";

        public const string PositionName = "position";

        public const string TargetName = "target";

        public const string UpVectorName = "upVector";

        public const string FovName = "fov";

        public const int LeastCameraChars = 75;

        /// <summary><paramref name="builder"/> はVMEを作る相手を返す。</summary>
        public static void AddTo(McpMethodTable methods, Func<object> builder)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            AddTo(methods, new SdkCameraSource(builder));
        }

        internal static void AddTo(McpMethodTable methods, ICameraSource source)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            methods.Add(ToolName, context => Run(context, source));
        }

        private static object Run(McpMethodContext context, ICameraSource source)
        {
            string unknown = context.Params.Keys
                .Where(n => n != VmdName && n != FramesName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument, "知らない項目を渡している: " + unknown);
            }

            object given;
            long id;
            if (!context.Params.TryGetValue(VmdName, out given) || !ValueInput.TryInteger(given, out id))
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument, VmdName + " は VMD のハンドルの整数でなければならない。");
            }

            IList<int> frames;
            string message;
            if (!TryFrames(context, out frames, out message))
            {
                return ToolEnvelope.Failure(ToolEnvelope.InvalidArgument, message);
            }

            if (frames.Count > ResponseSize.ValueChars(context.BudgetChars) / LeastCameraChars)
            {
                return TooLarge();
            }

            object held;
            if (id < int.MinValue || id > int.MaxValue || !context.Handles.TryGet((int)id, out held))
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidHandle, VmdName + " に使えないハンドルを渡している: " + id);
            }

            CameraRead read = null;
            Exception failure = null;
            UiInvocation invocation = context.Ui.TryInvokeOnUi(() =>
            {
                try
                {
                    read = source.Read(held, frames);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            if (!invocation.DidRun)
            {
                return ToolFailure.Unavailable(invocation);
            }

            if (failure != null)
            {
                return ToolFailure.Failed(failure, EditStage.BeforeCommit);
            }

            if (read.IsNotAMotion)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidHandle, VmdName + " が指すハンドルは VMD ではない: " + id);
            }

            if (!read.HasCamera)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable, "その VMD にはカメラのキーが無い: " + id);
            }

            object[] cameras = new object[frames.Count];
            for (int at = 0; at < cameras.Length; at++)
            {
                CameraSample sample = read.Samples[at];
                cameras[at] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { FrameName, frames[at] },
                    { PositionName, Triple(sample.Position) },
                    { TargetName, Triple(sample.Target) },
                    { UpVectorName, Triple(sample.Up) },
                    { FovName, (double)sample.Fov },
                };
            }

            Dictionary<string, object> value =
                new Dictionary<string, object>(StringComparer.Ordinal) { { CamerasName, cameras } };
            if (!ResponseSize.Fits(value, context.BudgetChars))
            {
                return TooLarge();
            }

            return ToolEnvelope.Success(value);
        }

        private static IDictionary<string, object> TooLarge()
        {
            return ToolEnvelope.Failure(
                ToolEnvelope.ResponseTooLarge,
                FramesName + " の数が多く、カメラの値が応答の枠に収まらない。" + FramesName + " を減らす。");
        }

        private static object[] Triple(float[] values)
        {
            return new object[] { (double)values[0], (double)values[1], (double)values[2] };
        }

        private static bool TryFrames(McpMethodContext context, out IList<int> frames, out string message)
        {
            frames = null;
            message = FramesName + " は0以上の整数を並べた、空でない配列でなければならない。";
            object given;
            object[] items = context.Params.TryGetValue(FramesName, out given) ? given as object[] : null;
            if (items == null || items.Length == 0)
            {
                return false;
            }

            List<int> taken = new List<int>();
            foreach (object item in items)
            {
                int number;
                if (!ValueInput.TryIndex(item, out number) || number < 0)
                {
                    return false;
                }

                taken.Add(number);
            }

            frames = taken;
            message = null;

            return true;
        }

        private sealed class SdkCameraSource : ICameraSource
        {
            private readonly Func<object> _builder;

            internal SdkCameraSource(Func<object> builder)
            {
                _builder = builder;
            }

            public CameraRead Read(object held, IList<int> frames)
            {
                IPEVmd vmd = held as IPEVmd;
                if (vmd == null)
                {
                    return CameraRead.NotAMotion;
                }

                IPEVme vme = ((IPEBuilder)_builder()).CreateVme();
                vme.Init();
                vme.SetVmd(vmd);
                if (!vme.Camera.Enable)
                {
                    return CameraRead.None;
                }

                int last = vmd.Camera.Max(key => ((IPEVmdFrameKey)key).FrameIndex);
                IPEVmeResult result = vme.Run(0, Math.Min(frames.Max(), last));
                if (result == null || !result.EnableCamera)
                {
                    return CameraRead.None;
                }

                CameraSample[] samples = new CameraSample[frames.Count];
                for (int at = 0; at < samples.Length; at++)
                {
                    IPEVmeCameraResult camera = result.GetCameraResult(Math.Min(frames[at], last));
                    if (camera == null)
                    {
                        return CameraRead.None;
                    }

                    samples[at] = new CameraSample(
                        Values(camera.Pos), Values(camera.Tgt), Values(camera.Up), camera.FOV);
                }

                return CameraRead.Found(samples);
            }

            private static float[] Values(Vector3 value)
            {
                return new[] { value.X, value.Y, value.Z };
            }
        }
    }

    internal interface ICameraSource
    {
        CameraRead Read(object vmd, IList<int> frames);
    }

    internal sealed class CameraRead
    {
        private CameraRead(CameraSample[] samples, bool notAMotion)
        {
            Samples = samples;
            IsNotAMotion = notAMotion;
        }

        internal static CameraRead None
        {
            get { return new CameraRead(null, false); }
        }

        internal static CameraRead NotAMotion
        {
            get { return new CameraRead(null, true); }
        }

        internal bool IsNotAMotion { get; }

        internal bool HasCamera
        {
            get { return Samples != null; }
        }

        internal CameraSample[] Samples { get; }

        internal static CameraRead Found(CameraSample[] samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            return new CameraRead(samples, false);
        }
    }

    internal sealed class CameraSample
    {
        internal CameraSample(float[] position, float[] target, float[] up, float fov)
        {
            Position = position;
            Target = target;
            Up = up;
            Fov = fov;
        }

        internal float[] Position { get; }

        internal float[] Target { get; }

        internal float[] Up { get; }

        internal float Fov { get; }
    }
}
