using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ViewImageRedrawTests
    {
        [Fact]
        public void TheViewIsDrawnAgainBeforeItsImageIsTaken()
        {
            List<string> ran = new List<string>();
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { ViewImageRedraw.ImageRow, (target, arguments) => Ran(ran, "image") },
                { ViewImageRedraw.RedrawRow, (target, arguments) => Ran(ran, "redraw") },
            };

            ViewImageRedraw.Fit(calls);
            calls[ViewImageRedraw.ImageRow](null, new object[0]);

            Assert.Equal(new[] { "redraw", "image" }, ran);
        }

        [Fact]
        public void TheImageThatTheMemberGivesBackIsWhatComesOut()
        {
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { ViewImageRedraw.ImageRow, (target, arguments) => "とった" },
                { ViewImageRedraw.RedrawRow, (target, arguments) => null },
            };

            ViewImageRedraw.Fit(calls);

            Assert.Equal("とった", calls[ViewImageRedraw.ImageRow](null, new object[0]));
        }

        [Fact]
        public void TheSameTargetIsHandedToBoth()
        {
            List<object> taken = new List<object>();
            object receiver = new object();
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { ViewImageRedraw.ImageRow, (target, arguments) => Took(taken, target) },
                { ViewImageRedraw.RedrawRow, (target, arguments) => Took(taken, target) },
            };

            ViewImageRedraw.Fit(calls);
            calls[ViewImageRedraw.ImageRow](receiver, new object[0]);

            Assert.Equal(new[] { receiver, receiver }, taken);
        }

        [Fact]
        public void ARowThatDoesNotTakeAnImageIsLeftAsItWas()
        {
            SdkCall untouched = (target, arguments) => "そのまま";
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { "Sdk.Type.Other()", untouched },
            };

            ViewImageRedraw.Fit(calls);

            Assert.Same(untouched, calls["Sdk.Type.Other()"]);
            Assert.Single(calls);
        }

        [Fact]
        public void TheImageRowIsLeftAsItWasWhenTheRedrawRowIsNotThere()
        {
            SdkCall untouched = (target, arguments) => "そのまま";
            Dictionary<string, SdkCall> calls = new Dictionary<string, SdkCall>(StringComparer.Ordinal)
            {
                { ViewImageRedraw.ImageRow, untouched },
            };

            ViewImageRedraw.Fit(calls);

            Assert.Same(untouched, calls[ViewImageRedraw.ImageRow]);
        }

        private static object Ran(IList<string> ran, string name)
        {
            ran.Add(name);

            return null;
        }

        private static object Took(IList<object> taken, object target)
        {
            taken.Add(target);

            return null;
        }
    }
}
