using System;
using System.Collections.Generic;
using PEPlugin.Vme;
using SlimDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VmePathPointsTests
    {
        [Theory]
        [InlineData(PEVmePathType.Spline, 0, true)]
        [InlineData(PEVmePathType.Spline, 1, false)]
        [InlineData(PEVmePathType.Spline, 2, false)]
        [InlineData(PEVmePathType.Spline, 3, true)]
        [InlineData(PEVmePathType.Linear, 0, true)]
        [InlineData(PEVmePathType.Linear, 1, false)]
        [InlineData(PEVmePathType.Linear, 2, true)]
        public void APathIsAcceptedOnlyWithEnoughPointsForItsType(
            PEVmePathType type, int points, bool accepted)
        {
            string message;

            Assert.Equal(accepted, VmePathPoints.TryAccept(type, points, out message));
            Assert.Equal(accepted, message == null);
        }

        [Fact]
        public void ThePointsOnAPathAreCounted()
        {
            Assert.Equal(0, VmePathPoints.Count(new FakeVmePath(PEVmePathType.Spline, 0)));
            Assert.Equal(5, VmePathPoints.Count(new FakeVmePath(PEVmePathType.Spline, 5)));
        }

        [Fact]
        public void SettingTooFewPointsOnASplineIsRefused()
        {
            string code;
            string message;

            Assert.False(VmePathPoints.TryCall(
                VmePathPoints.SetPointKey,
                new FakeVmePath(PEVmePathType.Spline, 0),
                new object[] { new Vector3[2] },
                out code,
                out message));
            Assert.Contains("3", message);
            Assert.True(VmePathPoints.TryCall(
                VmePathPoints.SetPointKey,
                new FakeVmePath(PEVmePathType.Spline, 0),
                new object[] { new Vector3[3] },
                out code,
                out message));
        }

        [Fact]
        public void AddingAPointIsRefusedUntilTheSplineCanHaveEnough()
        {
            string code;
            string message;

            Assert.False(VmePathPoints.TryCall(
                VmePathPoints.AddPointKey,
                new FakeVmePath(PEVmePathType.Spline, 1),
                new object[] { new Vector3() },
                out code,
                out message));
            Assert.True(VmePathPoints.TryCall(
                VmePathPoints.AddPointKey,
                new FakeVmePath(PEVmePathType.Spline, 2),
                new object[] { new Vector3() },
                out code,
                out message));
        }

        [Fact]
        public void SwitchingToASplineWithTooFewPointsIsRefused()
        {
            string message;

            Assert.False(VmePathPoints.TryWrite(
                VmePathPoints.PathTypeKey,
                new FakeVmePath(PEVmePathType.Linear, 2),
                PEVmePathType.Spline,
                out message));
            Assert.True(VmePathPoints.TryWrite(
                VmePathPoints.PathTypeKey,
                new FakeVmePath(PEVmePathType.Linear, 3),
                PEVmePathType.Spline,
                out message));
        }

        [Fact]
        public void OtherCallsAndOtherItemsPass()
        {
            string code;
            string message;

            Assert.True(VmePathPoints.TryCall(
                "PEPlugin.Vme.IPEVmePath.Clear()",
                new FakeVmePath(PEVmePathType.Spline, 1),
                new object[0],
                out code,
                out message));
            Assert.True(VmePathPoints.TryCall(
                VmePathPoints.SetPointKey,
                new object(),
                new object[] { new Vector3[1] },
                out code,
                out message));
            Assert.True(VmePathPoints.TryWrite(
                VmePathPoints.PathTypeKey, new object(), PEVmePathType.Spline, out message));
        }

        [Fact]
        public void ReadingPointsFromAPathWithNoPointsIsRefused()
        {
            string code;
            string message;

            Assert.False(VmePathPoints.TryCall(
                VmePathPoints.GetPathPointsKey,
                new FakeVmePath(PEVmePathType.Spline, 0),
                new object[] { 1.0 },
                out code,
                out message));
            Assert.Equal(ToolEnvelope.NotApplicable, code);
            Assert.Contains("点を置いてから", message);
            Assert.True(VmePathPoints.TryCall(
                VmePathPoints.GetPathPointsKey,
                new FakeVmePath(PEVmePathType.Spline, 3),
                new object[] { 1.0 },
                out code,
                out message));
        }

        [Fact]
        public void TheStartOfTheRangeOfAPathWithNoPointsIsZero()
        {
            object value;

            Assert.True(VmePathPoints.TryRead(
                VmePathPoints.RangeMinKey, new FakeVmePath(PEVmePathType.Spline, 0), out value));
            Assert.Equal(0.0, value);
            Assert.False(VmePathPoints.TryRead(
                VmePathPoints.RangeMinKey, new FakeVmePath(PEVmePathType.Spline, 3), out value));
            Assert.False(VmePathPoints.TryRead(
                "PEPlugin.Vme.IPEVmePath.RangeMax()",
                new FakeVmePath(PEVmePathType.Spline, 0),
                out value));
        }

        private sealed class FakeVmePath : IPEVmePath
        {
            private readonly int _points;

            public FakeVmePath(PEVmePathType type, int points)
            {
                PathType = type;
                _points = points;
            }

            public PEVmePathType PathType { get; set; }

            public double RangeMin
            {
                get { throw new NotSupportedException(); }
            }

            public double RangeMax
            {
                get { throw new NotSupportedException(); }
            }

            public double GetDistanceAtPoint(int pos)
            {
                return pos < 0 || pos >= _points ? -1.0 : pos;
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public void SetPoint(Vector3[] pos)
            {
                throw new NotSupportedException();
            }

            public void AddPoint(Vector3 pos)
            {
                throw new NotSupportedException();
            }

            public Vector3 GetPathPoint(double d)
            {
                throw new NotSupportedException();
            }

            public Vector3[] GetPathPoints(double v)
            {
                throw new NotSupportedException();
            }

            public Vector3[] GetPathPoints(double st, double ed, double v)
            {
                throw new NotSupportedException();
            }

            public Vector3[] GetPathPoints(Func<double, double> velProc)
            {
                throw new NotSupportedException();
            }

            public Vector3[] GetPathPoints(double st, double ed, Func<double, double> velProc)
            {
                throw new NotSupportedException();
            }

            public Vector3[] GetPathPoints(int frameSt, int frameEd, Func<int, double> proc)
            {
                throw new NotSupportedException();
            }

            public void SetPointFromBone(PEPlugin.Pmd.IPEPmd pmd, int maxCount = -1)
            {
                throw new NotSupportedException();
            }

            public void SetPointFromVertex(PEPlugin.Pmd.IPEPmd pmd, int maxCount = -1)
            {
                throw new NotSupportedException();
            }

            public void RegisterPositionEvent(
                IPEVmePositionEventOperator op, int frame, Vector3[] pos, int id = 0)
            {
                throw new NotSupportedException();
            }

            public void RegisterDirectionEvent(
                IPEVmeDirectionEventOperator op, int frame, Vector3[] dir, int id = 0)
            {
                throw new NotSupportedException();
            }

            public Vector3[] CreateFrontDirections(
                Vector3[] pos, int interval, bool sameLength = true)
            {
                throw new NotSupportedException();
            }
        }
    }
}
