/*
This file is part of MatterSlice. A commandline utility for
generating 3D printing GCode.

Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MatterHackers.VectorMath;
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	// A SimpleFace is a 3 dimensional model triangle with 3 points. These points are already converted to integers
	public class SimpleFace
	{
		public IntPoint[] vertices = new IntPoint[3];

		public SimpleFace(IntPoint v0, IntPoint v1, IntPoint v2)
		{
			vertices[0] = v0; vertices[1] = v1; vertices[2] = v2;
		}
	}

;

	// A SimpleVolume is the most basic representation of a 3D model. It contains all the faces as SimpleTriangles, with nothing fancy.
	public class SimpleMesh
	{
		public List<SimpleFace> faceTriangles = new List<SimpleFace>();

		public void addFaceTriangle(IntPoint v0, IntPoint v1, IntPoint v2)
		{
			faceTriangles.Add(new SimpleFace(v0, v1, v2));
		}

		public IntPoint maxXYZ_um()
		{
			if (faceTriangles.Count < 1)
			{
				return new IntPoint(0, 0, 0);
			}

			IntPoint ret = faceTriangles[0].vertices[0];
			for (int i = 0; i < faceTriangles.Count; i++)
			{
				SET_MAX(ref ret.X, faceTriangles[i].vertices[0].X);
				SET_MAX(ref ret.Y, faceTriangles[i].vertices[0].Y);
				SET_MAX(ref ret.Z, faceTriangles[i].vertices[0].Z);
				SET_MAX(ref ret.X, faceTriangles[i].vertices[1].X);
				SET_MAX(ref ret.Y, faceTriangles[i].vertices[1].Y);
				SET_MAX(ref ret.Z, faceTriangles[i].vertices[1].Z);
				SET_MAX(ref ret.X, faceTriangles[i].vertices[2].X);
				SET_MAX(ref ret.Y, faceTriangles[i].vertices[2].Y);
				SET_MAX(ref ret.Z, faceTriangles[i].vertices[2].Z);
			}

			return ret;
		}

		public IntPoint minXYZ_um()
		{
			if (faceTriangles.Count < 1)
			{
				return new IntPoint(0, 0, 0);
			}

			IntPoint ret = faceTriangles[0].vertices[0];
			for (int faceIndex = 0; faceIndex < faceTriangles.Count; faceIndex++)
			{
				SET_MIN(ref ret.X, faceTriangles[faceIndex].vertices[0].X);
				SET_MIN(ref ret.Y, faceTriangles[faceIndex].vertices[0].Y);
				SET_MIN(ref ret.Z, faceTriangles[faceIndex].vertices[0].Z);
				SET_MIN(ref ret.X, faceTriangles[faceIndex].vertices[1].X);
				SET_MIN(ref ret.Y, faceTriangles[faceIndex].vertices[1].Y);
				SET_MIN(ref ret.Z, faceTriangles[faceIndex].vertices[1].Z);
				SET_MIN(ref ret.X, faceTriangles[faceIndex].vertices[2].X);
				SET_MIN(ref ret.Y, faceTriangles[faceIndex].vertices[2].Y);
				SET_MIN(ref ret.Z, faceTriangles[faceIndex].vertices[2].Z);
			}

			return ret;
		}

		private void SET_MAX(ref int n, int m)
		{
			if (m > n)
			{
				n = m;
			}
		}

		private void SET_MAX(ref long n, long m)
		{
			if (m > n)
			{
				n = m;
			}
		}

		private void SET_MIN(ref int n, int m)
		{
			if (m < n)
			{
				n = m;
			}
		}

		private void SET_MIN(ref long n, long m)
		{
			if (m < n)
			{
				n = m;
			}
		}
	}

	// A SimpleModel is a 3D model with 1 or more 3D volumes.
	public class SimpleMeshCollection
	{
		public List<SimpleMesh> SimpleMeshes = new List<SimpleMesh>();

		public static bool LoadModelFromFile(SimpleMeshCollection simpleModel, string filename, Matrix4X4 matrix)
		{
			if (!loadModelSTL_ascii(simpleModel, filename, matrix))
			{
				return loadModelSTL_binary(simpleModel, filename, matrix);
			}

			return true;
		}

		public static bool LoadModelFromStream(SimpleMeshCollection simpleModel, Stream stream, Matrix4X4 matrix)
		{
			if (!loadModelSTLStream_ascii(simpleModel, stream, matrix))
			{
				stream.Position = 0;
				return loadModelSTLStream_binary(simpleModel, stream, matrix);
			}

			return true;
		}

		public static bool loadModelSTLStream_ascii(SimpleMeshCollection simpleModel, Stream stream, Matrix4X4 matrix)
		{
			SimpleMesh vol = new SimpleMesh();
			using (StreamReader f = new StreamReader(stream))
			{
				// check for "SOLID"

				var vertex = default(MatterHackers.VectorMath.Vector3);
				int n = 0;
				IntPoint v0 = new IntPoint(0, 0, 0);
				IntPoint v1 = new IntPoint(0, 0, 0);
				IntPoint v2 = new IntPoint(0, 0, 0);
				string line = f.ReadLine();
				Regex onlySingleSpaces = new Regex("\\s+", RegexOptions.Compiled);
				int lineCount = 0;
				while (line != null)
				{
					if (lineCount++ > 100 && vol.faceTriangles.Count == 0)
					{
						return false;
					}

					line = onlySingleSpaces.Replace(line, " ");
					var parts = line.Trim().Split(' ');
					if (parts[0].Trim() == "vertex")
					{
						vertex.X = Convert.ToDouble(parts[1]);
						vertex.Y = Convert.ToDouble(parts[2]);
						vertex.Z = Convert.ToDouble(parts[3]);

						// change the scale from mm to micrometers
						n++;
						switch (n)
						{
							case 1:
								var new0 = VectorMath.Vector3Ex.Transform(vertex, matrix) * 1000;
								v0 = new IntPoint(new0.X, new0.Y, new0.Z);
								break;

							case 2:
								var new1 = VectorMath.Vector3Ex.Transform(vertex, matrix) * 1000;
								v1 = new IntPoint(new1.X, new1.Y, new1.Z);
								break;

							case 3:
								var new2 = VectorMath.Vector3Ex.Transform(vertex, matrix) * 1000;
								v2 = new IntPoint(new2.X, new2.Y, new2.Z);
								vol.addFaceTriangle(v0, v1, v2);
								n = 0;
								break;
						}
					}

					line = f.ReadLine();
				}
			}

			if (vol.faceTriangles.Count > 3)
			{
				simpleModel.SimpleMeshes.Add(vol);
				return true;
			}

			return false;
		}

		public static bool loadModelSTL_ascii(SimpleMeshCollection simpleModel, string filename, Matrix4X4 matrix)
		{
			SimpleMesh vol = new SimpleMesh();
			using (StreamReader f = new StreamReader(filename))
			{
				// check for "SOLID"

				var vertex = default(MatterHackers.VectorMath.Vector3);
				int n = 0;
				IntPoint v0 = new IntPoint(0, 0, 0);
				IntPoint v1 = new IntPoint(0, 0, 0);
				IntPoint v2 = new IntPoint(0, 0, 0);
				string line = f.ReadLine();
				Regex onlySingleSpaces = new Regex("\\s+", RegexOptions.Compiled);
				int lineCount = 0;
				while (line != null)
				{
					if (lineCount++ > 100 && vol.faceTriangles.Count == 0)
					{
						return false;
					}

					line = onlySingleSpaces.Replace(line, " ");
					var parts = line.Trim().Split(' ');
					if (parts[0].Trim() == "vertex")
					{
						vertex.X = Convert.ToDouble(parts[1]);
						vertex.Y = Convert.ToDouble(parts[2]);
						vertex.Z = Convert.ToDouble(parts[3]);

						// change the scale from mm to micrometers
						n++;
						switch (n)
						{
							case 1:
								var new0 = VectorMath.Vector3Ex.Transform(vertex, matrix) * 1000;
								v0 = new IntPoint(new0.X, new0.Y, new0.Z);
								break;

							case 2:
								var new1 = VectorMath.Vector3Ex.Transform(vertex, matrix) * 1000;
								v1 = new IntPoint(new1.X, new1.Y, new1.Z);
								break;

							case 3:
								var new2 = VectorMath.Vector3Ex.Transform(vertex, matrix) * 1000;
								v2 = new IntPoint(new2.X, new2.Y, new2.Z);
								vol.addFaceTriangle(v0, v1, v2);
								n = 0;
								break;
						}
					}

					line = f.ReadLine();
				}
			}

			if (vol.faceTriangles.Count > 3)
			{
				simpleModel.SimpleMeshes.Add(vol);
				return true;
			}

			return false;
		}

		public IntPoint MaxXYZ_um()
		{
			if (SimpleMeshes.Count < 1)
			{
				return new IntPoint(0, 0, 0);
			}

			IntPoint maxXYZ = new IntPoint(long.MinValue, long.MinValue, long.MinValue);
			foreach (var mesh in SimpleMeshes)
			{
				if (mesh.faceTriangles.Count > 0)
				{
					IntPoint meshMaxXYZ = mesh.maxXYZ_um();
					maxXYZ.X = Math.Max(maxXYZ.X, meshMaxXYZ.X);
					maxXYZ.Y = Math.Max(maxXYZ.Y, meshMaxXYZ.Y);
					maxXYZ.Z = Math.Max(maxXYZ.Z, meshMaxXYZ.Z);
				}
			}

			return maxXYZ;
		}

		public IntPoint MinXYZ_um()
		{
			if (SimpleMeshes.Count < 1)
			{
				return new IntPoint(0, 0, 0);
			}

			IntPoint minXYZ = new IntPoint(long.MaxValue, long.MaxValue, long.MaxValue);
			foreach (var mesh in SimpleMeshes)
			{
				if (mesh.faceTriangles.Count > 0)
				{
					IntPoint meshMinXYZ = mesh.minXYZ_um();
					minXYZ.X = Math.Min(minXYZ.X, meshMinXYZ.X);
					minXYZ.Y = Math.Min(minXYZ.Y, meshMinXYZ.Y);
					minXYZ.Z = Math.Min(minXYZ.Z, meshMinXYZ.Z);
				}
			}

			return minXYZ;
		}

		private static bool loadModelSTLStream_binary(SimpleMeshCollection simpleModel, Stream stlStream, Matrix4X4 matrix)
		{
			SimpleMesh vol = new SimpleMesh();

			// load it as a binary stl
			// skip the first 80 bytes
			// read in the number of triangles
			stlStream.Position = 0;
			BinaryReader br = new BinaryReader(stlStream);
			byte[] fileContents = br.ReadBytes((int)stlStream.Length);
			int currentPosition = 80;
			if (fileContents.Length < currentPosition)
			{
				return false;
			}

			uint numTriangles = System.BitConverter.ToUInt32(fileContents, currentPosition);
			long bytesForNormals = numTriangles * 3 * 4;
			long bytesForVertices = numTriangles * 3 * 4;
			long bytesForAttributs = numTriangles * 2;
			currentPosition += 4;
			long numBytesRequiredForVertexData = currentPosition + bytesForNormals + bytesForVertices + bytesForAttributs;
			if (fileContents.Length < numBytesRequiredForVertexData || numTriangles < 0)
			{
				stlStream.Close();
				return false;
			}

			IntPoint[] vector = new IntPoint[3];
			for (int i = 0; i < numTriangles; i++)
			{
				// skip the normal
				currentPosition += 3 * 4;
				for (int j = 0; j < 3; j++)
				{
					var vertex = new MatterHackers.VectorMath.Vector3(
						System.BitConverter.ToSingle(fileContents, currentPosition + 0 * 4),
						System.BitConverter.ToSingle(fileContents, currentPosition + 1 * 4),
						System.BitConverter.ToSingle(fileContents, currentPosition + 2 * 4));

					var new0 = VectorMath.Vector3Ex.Transform(vertex, matrix);
					vector[j] = new IntPoint(new0.X * 1000, new0.Y * 1000, new0.Z * 1000);
					currentPosition += 3 * 4;
				}

				currentPosition += 2; // skip the attribute

				vol.addFaceTriangle(vector[2], vector[1], vector[0]);
			}


			// Detect and skip non-visible mesh
			var bounds = vol.maxXYZ_um() - vol.minXYZ_um();

			if (vol.faceTriangles.Count > 0)
			{
				if (bounds.X == 0)
				{
					vol.faceTriangles = new List<SimpleFace>();
				}

				simpleModel.SimpleMeshes.Add(vol);
				return true;
			}

			return false;
		}

		private static bool loadModelSTL_binary(SimpleMeshCollection simpleModel, string filename, Matrix4X4 matrix)
		{
			SimpleMesh vol = new SimpleMesh();
			using (FileStream stlStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				// load it as a binary stl
				// skip the first 80 bytes
				// read in the number of triangles
				stlStream.Position = 0;
				BinaryReader br = new BinaryReader(stlStream);
				byte[] fileContents = br.ReadBytes((int)stlStream.Length);
				int currentPosition = 80;
				if (fileContents.Length < currentPosition)
				{
					return false;
				}

				uint numTriangles = System.BitConverter.ToUInt32(fileContents, currentPosition);
				long bytesForNormals = numTriangles * 3 * 4;
				long bytesForVertices = numTriangles * 3 * 4;
				long bytesForAttributs = numTriangles * 2;
				currentPosition += 4;
				long numBytesRequiredForVertexData = currentPosition + bytesForNormals + bytesForVertices + bytesForAttributs;
				if (fileContents.Length < numBytesRequiredForVertexData || numTriangles < 0)
				{
					stlStream.Close();
					return false;
				}

				IntPoint[] vector = new IntPoint[3];
				for (int i = 0; i < numTriangles; i++)
				{
					// skip the normal
					currentPosition += 3 * 4;
					for (int j = 0; j < 3; j++)
					{
						var vertex = new MatterHackers.VectorMath.Vector3(
							System.BitConverter.ToSingle(fileContents, currentPosition + 0 * 4),
							System.BitConverter.ToSingle(fileContents, currentPosition + 1 * 4),
							System.BitConverter.ToSingle(fileContents, currentPosition + 2 * 4));

						var new0 = VectorMath.Vector3Ex.Transform(vertex, matrix);
						vector[j] = new IntPoint(new0.X * 1000, new0.Y * 1000, new0.Z * 1000);
						currentPosition += 3 * 4;
					}

					currentPosition += 2; // skip the attribute

					vol.addFaceTriangle(vector[2], vector[1], vector[0]);
				}
			}

			// Detect and skip non-visible mesh
			var bounds = vol.maxXYZ_um() - vol.minXYZ_um();

			if (vol.faceTriangles.Count > 0)
			{
				if (bounds.X == 0)
				{
					vol.faceTriangles = new List<SimpleFace>();
				}

				simpleModel.SimpleMeshes.Add(vol);
				return true;
			}

			return false;
		}
	}
}