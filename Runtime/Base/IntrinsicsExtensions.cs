/*
	Copyright © Carl Emil Carlsen 2023-2024
	http://cec.dk
*/

using com.rfilkov.kinect;
using UnityEngine;


namespace TrackingTools
{
	public static class IntrinsicsExtensions
	{

		public static bool UpdateFromAzureKinectExamples( this Intrinsics i, KinectInterop.CameraIntrinsics intrinsics )
		{
			if( intrinsics == null ) {
				Debug.LogError( "No valid Intrinsics data.\n");
				return false;
			}
			int distValueCount = intrinsics.distCoeffs.Length;
			i.UpdateRaw
			(
				intrinsics.width, intrinsics.height,
				intrinsics.ppx, intrinsics.ppy,
				intrinsics.fx, intrinsics.fy,
				distValueCount > 0 ? intrinsics.distCoeffs[ 0 ] : 0f,
				distValueCount > 1 ? intrinsics.distCoeffs[ 1 ] : 0f,
				distValueCount > 2 ? intrinsics.distCoeffs[ 2 ] : 0f,
				distValueCount > 3 ? intrinsics.distCoeffs[ 3 ] : 0f,
				distValueCount > 4 ? intrinsics.distCoeffs[ 4 ] : 0f,
				distValueCount > 5 ? intrinsics.distCoeffs[ 5 ] : 0f,
				intrinsics.p1, intrinsics.p2
			);
			return true;
		}

	}
}