using UnityEngine;
using System.Collections.Generic;

namespace Pathfinding.Util {
	/// <summary>Interpolates along a sequence of points</summary>
	public class PathInterpolator {
		private List<Vector3> path;

		private float distanceToSegmentStart;
		private float currentDistance;
		private float currentSegmentLength = float.PositiveInfinity;
		private float totalDistance = float.PositiveInfinity;

		/// <summary>Current position</summary>
		public virtual Vector3 position {
			get {
				float t = currentSegmentLength > 0.0001f ? (currentDistance - distanceToSegmentStart) / currentSegmentLength : 0f;
				return Vector3.Lerp(path[segmentIndex], path[segmentIndex+1], t);
			}
		}

		/// <summary>Last point in the path</summary>
		public Vector3 endPoint {
			get {
				return path[path.Count-1];
			}
		}

		/// <summary>Tangent of the curve at the current position</summary>
		public Vector3 tangent {
			get {
				return path[segmentIndex+1] - path[segmentIndex];
			}
		}

		/// <summary>Remaining distance until the end of the path</summary>
		public float remainingDistance {
			get {
				return totalDistance - distance;
			}
			set {
				distance = totalDistance - value;
			}
		}

		/// <summary>Traversed distance from the start of the path</summary>
		public float distance {
			get {
				return currentDistance;
			}
			set {
				currentDistance = value;

				while (currentDistance < distanceToSegmentStart && segmentIndex > 0) PrevSegment();
				while (currentDistance > distanceToSegmentStart + currentSegmentLength && segmentIndex < path.Count - 2) NextSegment();
			}
		}

		/// <summary>
		/// Current segment.
		/// The start and end points of the segment are path[value] and path[value+1].
		/// </summary>
		public int segmentIndex { get; private set; }

		/// <summary>
		/// True if this instance has a path set.
		/// See: SetPath
		/// </summary>
		public bool valid {
			get {
				return path != null;
			}
		}

		/// <summary>Appends the remaining path between <see cref="position"/> and <see cref="endPoint"/> to buffer</summary>
		public void GetRemainingPath (List<Vector3> buffer) {
			if (!valid) throw new System.Exception("PathInterpolator is not valid");
			buffer.Add(position);
			for (int i = segmentIndex+1; i < path.Count; i++) {
				buffer.Add(path[i]);
			}
		}

		/// <summary>
		/// Set the path to interpolate along.
		/// This will reset all interpolation variables.
		/// </summary>
		public void SetPath (List<Vector3> path) {
			this.path = path;
			currentDistance = 0;
			segmentIndex = 0;
			distanceToSegmentStart = 0;

			if (path == null) {
				totalDistance = float.PositiveInfinity;
				currentSegmentLength = float.PositiveInfinity;
				return;
			}

			if (path.Count < 2) throw new System.ArgumentException("Path must have a length of at least 2");

			currentSegmentLength = (path[1] - path[0]).magnitude;
			totalDistance = 0f;

			var prev = path[0];
			for (int i = 1; i < path.Count; i++) {
				var current = path[i];
				totalDistance += (current - prev).magnitude;
				prev = current;
			}
		}

		/// <summary>Move to the specified segment and move a fraction of the way to the next segment</summary>
		public void MoveToSegment (int index, float fractionAlongSegment) {
			if (path == null) return;
			if (index < 0 || index >= path.Count - 1) throw new System.ArgumentOutOfRangeException("index");
			while (segmentIndex > index) PrevSegment();
			while (segmentIndex < index) NextSegment();
			distance = distanceToSegmentStart + Mathf.Clamp01(fractionAlongSegment) * currentSegmentLength;
		}

		/// <summary>Move as close as possible to the specified point</summary>
		public void MoveToClosestPoint (Vector3 point) {
			if (path == null) return;

			float bestDist = float.PositiveInfinity;
			float bestFactor = 0f;
			int bestIndex = 0;

			for (int i = 0; i < path.Count-1; i++) {
				float factor = VectorMath.ClosestPointOnLineFactor(path[i], path[i+1], point);
				Vector3 closest = Vector3.Lerp(path[i], path[i+1], factor);
				float dist = (point - closest).sqrMagnitude;

				if (dist < bestDist) {
					bestDist = dist;
					bestFactor = factor;
					bestIndex = i;
				}
			}

			MoveToSegment(bestIndex, bestFactor);
		}

		public void MoveToLocallyClosestPoint (Vector3 point, bool allowForwards = true, bool allowBackwards = true) {
			if (path == null) return;

			while (allowForwards && segmentIndex < path.Count - 2 && (path[segmentIndex+1] - point).sqrMagnitude <= (path[segmentIndex] - point).sqrMagnitude) {
				NextSegment();
			}

			while (allowBackwards && segmentIndex > 0 && (path[segmentIndex-1] - point).sqrMagnitude <= (path[segmentIndex] - point).sqrMagnitude) {
				PrevSegment();
			}

			// Check the distances to the two segments extending from the vertex path[segmentIndex]
			// and pick the position on those segments that is closest to the #point parameter.
			float factor1 = 0, factor2 = 0, d1 = float.PositiveInfinity, d2 = float.PositiveInfinity;
			if (segmentIndex > 0) {
				factor1 = VectorMath.ClosestPointOnLineFactor(path[segmentIndex-1], path[segmentIndex], point);
				d1 = (Vector3.Lerp(path[segmentIndex-1], path[segmentIndex], factor1) - point).sqrMagnitude;
			}

			if (segmentIndex < path.Count - 1) {
				factor2 = VectorMath.ClosestPointOnLineFactor(path[segmentIndex], path[segmentIndex+1], point);
				d2 = (Vector3.Lerp(path[segmentIndex], path[segmentIndex+1], factor2) - point).sqrMagnitude;
			}

			if (d1 < d2) MoveToSegment(segmentIndex - 1, factor1);
			else MoveToSegment(segmentIndex, factor2);
		}

		public void MoveToCircleIntersection2D (Vector3 circleCenter3D, float radius, IMovementPlane transform) {
			if (path == null) return;

			// Move forwards as long as we are getting closer to circleCenter3D
			while (segmentIndex < path.Count - 2 && VectorMath.ClosestPointOnLineFactor(path[segmentIndex], path[segmentIndex+1], circleCenter3D) > 1) {
				NextSegment();
			}

			var circleCenter = transform.ToPlane(circleCenter3D);

			// Move forwards as long as the current segment endpoint is within the circle
			while (segmentIndex < path.Count - 2 && (transform.ToPlane(path[segmentIndex+1]) - circleCenter).sqrMagnitude <= radius*radius) {
				NextSegment();
			}

			// Calculate the intersection with the circle. This involves some math.
			var factor = VectorMath.LineCircleIntersectionFactor(circleCenter, transform.ToPlane(path[segmentIndex]), transform.ToPlane(path[segmentIndex+1]), radius);
			// Move to the intersection point
			MoveToSegment(segmentIndex, factor);
		}

		protected virtual void PrevSegment () {
			segmentIndex--;
			currentSegmentLength = (path[segmentIndex+1] - path[segmentIndex]).magnitude;
			distanceToSegmentStart -= currentSegmentLength;
		}

		protected virtual void NextSegment () {
			segmentIndex++;
			distanceToSegmentStart += currentSegmentLength;
			currentSegmentLength = (path[segmentIndex+1] - path[segmentIndex]).magnitude;
		}
	}
}
