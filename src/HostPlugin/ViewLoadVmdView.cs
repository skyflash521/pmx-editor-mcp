using System;
using System.Collections.Generic;
using PEPlugin;
using PEPlugin.Pmx;
using PEPlugin.View;
using PEPlugin.Vmd;

namespace PmxEditorMcp
{
    public static class ViewLoadVmdView
    {
        public const string ToolName = "view_load_vmd_view";

        public const string PartsName = "parts";

        public const string ModelOnly = "modelOnly";

        public const string WholeMotion = "wholeMotion";

        public const string ModelMotion = "modelMotion";

        public const string CameraMotion = "cameraMotion";

        public const string LightMotion = "lightMotion";

        public static IList<string> Parts
        {
            get
            {
                return new[] { ModelOnly, WholeMotion, ModelMotion, CameraMotion, LightMotion };
            }
        }

        public const string MotionPathName = "motionPath";

        public const string ModelPathName = "modelPath";

        public const string BootedName = "booted";

        /// <summary>ツールを表へ足す。<paramref name="builder"/> はVMDやPMXを作る相手を返す。</summary>
        public static void AddTo(
            McpMethodTable methods, ComposedScreen screen, Func<object> builder)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            List<string> known = new List<string> { PartsName, MotionPathName, ModelPathName };
            methods.Add(
                ToolName,
                screen.Method(
                    known,
                    ScreenNeeds.View | ScreenNeeds.Pmx,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, parts, builder)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, ScreenParts parts, Func<object> builder)
        {
            string wanted;
            string motionPath;
            string modelPath;
            string code;
            string message;
            if (!ComposedInput.TryChoice(context, PartsName, Parts, out wanted, out code, out message)
                || !ComposedInput.TryText(
                    context,
                    MotionPathName,
                    wanted,
                    Motions,
                    out motionPath,
                    out code,
                    out message)
                || !TryModelPath(context, out modelPath, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IPEBuilder made = (IPEBuilder)builder();
            IPEVmd held = null;
            if (motionPath != null)
            {
                held = made.CreateVmd();
                held.FromFile(motionPath);
                Kept(held, wanted);
            }

            IPXPmxViewConnector view = (IPXPmxViewConnector)parts.View;
            if (Older(modelPath))
            {
                view.BootupVmdView(made.CreatePmd(modelPath), held);
            }
            else
            {
                view.BootupVmdView(Model(made, parts, modelPath), held);
            }

            if (held != null)
            {
                view.PlayVmdView();
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { BootedName, view.IsVmdViewBootup },
                });
        }

        /// <summary>その道がPMDのファイルを指しているか。</summary>
        private static bool Older(string path)
        {
            return path != null
                && path.EndsWith(OlderTail, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>読み込むPMX。道を渡されていなければ、いま編集しているモデル。</summary>
        private static IPXPmx Model(IPEBuilder builder, ScreenParts parts, string path)
        {
            if (path == null)
            {
                return (IPXPmx)parts.Pmx;
            }

            IPXPmx made = builder.Pmx.Pmx();
            made.FromFile(path);

            return made;
        }

        private const string OlderTail = ".pmd";

        private static IList<string> Motions
        {
            get { return new[] { WholeMotion, ModelMotion, CameraMotion, LightMotion }; }
        }

        /// <summary>指した持ち物のキーだけを残す。</summary>
        private static void Kept(IPEVmd motion, string wanted)
        {
            if (string.Equals(wanted, WholeMotion, StringComparison.Ordinal))
            {
                return;
            }

            bool model = string.Equals(wanted, ModelMotion, StringComparison.Ordinal);
            bool camera = string.Equals(wanted, CameraMotion, StringComparison.Ordinal);
            if (!model)
            {
                motion.Bone.Clear();
                motion.Morph.Clear();
                motion.VisibleIK.Clear();
            }

            if (!camera)
            {
                motion.Camera.Clear();
            }

            if (model || camera)
            {
                motion.Light.Clear();
                motion.SelfShadow.Clear();
            }
        }

        /// <summary>渡されていなければ空を渡し、いま編集しているモデルを読み込む相手にする。</summary>
        private static bool TryModelPath(
            McpMethodContext context, out string path, out string code, out string message)
        {
            path = null;
            code = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(ModelPathName, out given))
            {
                return true;
            }

            path = given as string;
            if (!string.IsNullOrEmpty(path))
            {
                return true;
            }

            path = null;
            code = ToolEnvelope.InvalidArgument;
            message = ModelPathName + " は空でない文字である。";

            return false;
        }
    }
}
