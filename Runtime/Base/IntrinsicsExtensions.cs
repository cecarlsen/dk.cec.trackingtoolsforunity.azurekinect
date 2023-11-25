/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using com.rfilkov.kinect;


namespace TrackingTools
{
	public static class IntrinsicsExtensions
	{

		public static void UpdateFromAzureKinectExamples( this Intrinsics i, KinectInterop.CameraIntrinsics intrinsics )
		{
			i.UpdateRaw
			(
				intrinsics.width, intrinsics.height,
				intrinsics.ppx, intrinsics.ppy,
				intrinsics.fx, intrinsics.fy,
				intrinsics.distCoeffs[0], intrinsics.distCoeffs[1], intrinsics.distCoeffs[2], intrinsics.distCoeffs[3], intrinsics.distCoeffs[4], intrinsics.distCoeffs[5],
				intrinsics.p1, intrinsics.p2
			);
		}

	}
}