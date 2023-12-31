﻿/*
	Copyright © Carl Emil Carlsen 2023-2024
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Events;
using com.rfilkov.kinect;
using UnityEngine.Experimental.Rendering;
using System;

namespace TrackingTools.AzureKinect
{
	public class AzureKinectTextureProvider : CameraTextureProvider
	{
		// Setup.
		[SerializeField] int _sensorId = 0;
		[SerializeField] Stream _stream = Stream.Color;

		// Parameters.
		[SerializeField] bool _undistort = false;
		[SerializeField] bool _flipVertically = false;

		// Events.
		[SerializeField] UnityEvent<Texture> _latestTextureEvent = new UnityEvent<Texture>();

		ulong _latestFrameTimeMicroSeconds;
		ulong _previousFrameTimeMicroSeconds;

		long _latestFrameNum = 0; // The kinect does not provide a frame numer, only time, so we do the counting ourselves.
		int _framesSinceLastUnityUpdate = 0;

		RenderTexture _processedTexture;
		Texture _latestTexture;

		Material _renderDepthTextureMaterial;
		RenderTexture _depthSourceTexture;
		Texture2D _infraredSourceTexture;
		byte[] _rawImageDataBytes;

		LensUndistorter _lensUndistorter;

		static Flipper flipper;


		bool process => _undistort || _flipVertically;

		/// <summary>
		/// Number of frames counted since last Unity update.
		/// </summary>
		public override int framesSinceLastUnityUpdate => _framesSinceLastUnityUpdate;

		/// <summary>
		/// Number of frames aquired and available since last Unity update.
		/// </summary>
		public override int framesAquiredSinceLastUnityUpdate => _framesSinceLastUnityUpdate;

		/// <summary>
		/// Interval between two latest frames in seconds.
		/// </summary>
		public override double latestFrameInterval => ( _latestFrameTimeMicroSeconds - _previousFrameTimeMicroSeconds ) * 0.0000001d;

		/// <summary>
		/// Get latest frame number.
		/// </summary>
		public override long latestFrameNumber => _latestFrameNum;


		[Serializable]
		public enum Stream { Color, Infrared, Depth }

		static class ShaderIDs
		{
			public static readonly int _DistMapTex = Shader.PropertyToID( nameof( _DistMapTex ) );
			public static readonly int _TexResX = Shader.PropertyToID( nameof( _TexResX ) );
			public static readonly int _TexResY = Shader.PropertyToID( nameof( _TexResY ) );
			public static readonly int _MinDepth = Shader.PropertyToID( nameof( _MinDepth ) );
			public static readonly int _MaxDepth = Shader.PropertyToID( nameof( _MaxDepth ) );
			public static readonly int _DepthMap = Shader.PropertyToID( nameof( _DepthMap ) );
		}


		/// <summary>
		/// Get latest aquired frame.
		/// </summary>
		public override Texture GetLatestTexture() => _latestTexture;


		/// <summary>
		/// Get texture at history index. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public override Texture GetHistoryTexture( int historyIndex )
		{
			if( historyIndex != 0 ) throw new System.Exception( "AzureKinectProvider only stores one frame." );

			return _latestTexture;
		}


		/// <summary>
		/// Get latest frame time in seconds, measured relative to capture begin time. 
		/// </summary>
		public override double GetLatestFrameTime() => _latestFrameTimeMicroSeconds * 0.0000001d;


		/// <summary>
		/// Get history frame time in seconds, measured relative to capture begin time. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public override double GetHistoryFrameTime( int historyIndex )
		{
			if( historyIndex != 0 ) throw new System.Exception( "AzureKinectProvider only stores one frame." );

			return _latestFrameTimeMicroSeconds * 0.0000001d;
		}


		void Awake()
		{
			if( flipper == null ) flipper = new Flipper();

			KinectManager kinectManager = KinectManager.Instance;
			if( !kinectManager ) return;
		}


		void OnEnable()
		{
			
		}


		void OnDisable()
		{
			_framesSinceLastUnityUpdate = 0;
		}


		void Update()
		{
			KinectManager kinectManager = KinectManager.Instance;
			if( !kinectManager || !kinectManager.IsInitialized() ) return;

			// Make sure KinectManager is set up correctly.
			switch( _stream )
			{
				case Stream.Color:
					if( kinectManager.getColorFrames == KinectManager.ColorTextureType.None ) kinectManager.getColorFrames = KinectManager.ColorTextureType.ColorTexture;
					break;
				case Stream.Infrared:
					if( kinectManager.getInfraredFrames != KinectManager.InfraredTextureType.None ) kinectManager.getInfraredFrames = KinectManager.InfraredTextureType.RawInfraredData;
					break;
				case Stream.Depth:
					if( kinectManager.getDepthFrames == KinectManager.DepthTextureType.None ) kinectManager.getDepthFrames = KinectManager.DepthTextureType.RawDepthData;
					break;
			}

			// Get the sensor data.
			KinectInterop.SensorData sensorData = kinectManager.GetSensorData( _sensorId );

			// Update texture.
			bool hasNewFrame = false;
			switch( _stream )
			{
				case Stream.Color: hasNewFrame = UpdateColorTexture( kinectManager, sensorData ); break;
				case Stream.Infrared: hasNewFrame = UpdateInfraredTexture( kinectManager, sensorData ); break;
				case Stream.Depth: hasNewFrame = UpdateDepthTexture( kinectManager, sensorData ); break;
			}

			if( hasNewFrame ) {
				_latestFrameNum++;
				_framesSinceLastUnityUpdate = 1;
				_latestTextureEvent.Invoke( _latestTexture );
			} else {
				_framesSinceLastUnityUpdate = 0;
			}
		}


		bool UpdateColorTexture( KinectManager kinectManager, KinectInterop.SensorData sensorData )
		{
			if( sensorData.lastColorFrameTime == _latestFrameTimeMicroSeconds ) return false;

			Texture colorTexture = kinectManager.GetColorImageTex( _sensorId );
			colorTexture.wrapMode = TextureWrapMode.Repeat;
			if( colorTexture ){
				if( string.IsNullOrEmpty( colorTexture.name ) ) colorTexture.name = "KinectColor";
			}

			if( process ) {
				if( !_processedTexture ) {
					_processedTexture = new RenderTexture( colorTexture.width, colorTexture.height, 0, colorTexture.graphicsFormat );
					_processedTexture.name = "KinectColorProcessed";
				}
			}

			if( _undistort ) EnsureUndistortResources( sensorData.colorCamIntr );

			if( process ) Process( colorTexture );

			_latestTexture = process ? _processedTexture : colorTexture;
			_previousFrameTimeMicroSeconds = _latestFrameTimeMicroSeconds;
			_latestFrameTimeMicroSeconds = sensorData.lastColorFrameTime;

			return true;
		}


		bool UpdateInfraredTexture( KinectManager kinectManager, KinectInterop.SensorData sensorData )
		{
			if( sensorData.lastInfraredFrameTime == _latestFrameTimeMicroSeconds ) return false;

			// Azure Kinect Examples loads IR as RGB24, but the IR is actually R16, so we loose a lot of information.
			// If you look IR in the official Azure Kinect Viewver it matches Azure Kinet Examples. They are very bright, and typically burned out.
			// We want the full spectrum, so we load 16bit directly to texture.

			int width = sensorData.depthImageWidth;
			int height = sensorData.depthImageHeight;
				
			// Create texture.
			if( !_infraredSourceTexture ) {
				int pixelCount = width * height;
				_infraredSourceTexture = new Texture2D( width, height, GraphicsFormat.R16_UNorm, TextureCreationFlags.None );
				_infraredSourceTexture.name = "KinectIR";
				_rawImageDataBytes = new byte[ pixelCount * 2 ];
			}

			// Get raw image data.
			ushort[] rawImageData = kinectManager.GetRawInfraredMap( _sensorId );

			// ushort[] to byte[].
			// https://stackoverflow.com/questions/37213819/convert-ushort-into-byte-and-back
			Buffer.BlockCopy( rawImageData, 0, _rawImageDataBytes, 0, rawImageData.Length * 2 );

			// Load into texture.
			_infraredSourceTexture.LoadRawTextureData( _rawImageDataBytes );
			_infraredSourceTexture.Apply();

			if( _infraredSourceTexture && string.IsNullOrEmpty( _infraredSourceTexture.name ) ) _infraredSourceTexture.name = "KinectIR";

			if( process ) {
				if( !_processedTexture ){
					_processedTexture = new RenderTexture( _infraredSourceTexture.width, _infraredSourceTexture.height, 0, _infraredSourceTexture.graphicsFormat );
					_processedTexture.name = "KinectIRProcessed";
				}
			}

			if( _undistort ) EnsureUndistortResources( sensorData.depthCamIntr );

			if( process ) Process( _infraredSourceTexture );

			_latestTexture = process ? _processedTexture : _infraredSourceTexture;
			_previousFrameTimeMicroSeconds = _latestFrameTimeMicroSeconds;
			_latestFrameTimeMicroSeconds = sensorData.lastInfraredFrameTime;

			return true;
		}


		bool UpdateDepthTexture( KinectManager kinectManager, KinectInterop.SensorData sensorData )
		{
			//Debug.Log( sensorData.startTimeOffset + " " + kinectManager.GetDepthFrameTime( 0 ) + " == " + sensorData.lastDepthFrameTime );

			if( sensorData.lastDepthFrameTime == _latestFrameTimeMicroSeconds ) return false;

			// Ensure we have a depth conversion material.
			if( !_renderDepthTextureMaterial ) {
				Shader shader = Shader.Find( "KinectAzureTextureProvider/KinectDepthShader" );
				_renderDepthTextureMaterial = new Material( shader );
			}

			// Don't use AzureKinectExamples depth. It is RGB888 and encodes depth to two-color hue.
			int w = kinectManager.GetDepthImageWidth( _sensorId );
			int h = kinectManager.GetDepthImageHeight( _sensorId );
			if( !_depthSourceTexture || _depthSourceTexture.width != w || _depthSourceTexture.height != h ){
				if( _depthSourceTexture ) _depthSourceTexture.Release();
				_depthSourceTexture = new RenderTexture( w, h, 0, RenderTextureFormat.RFloat );
				_depthSourceTexture.name = "KinectDepth";
				_depthSourceTexture.filterMode = _depthSourceTexture ? FilterMode.Point : FilterMode.Bilinear; // Don't interpolate depth values.
			}

			// Ensure that sensor data has a depthImageBuffer and set it's content.
			if( sensorData.depthImageBuffer == null){
				int depthBufferLength = sensorData.depthImageWidth * sensorData.depthImageHeight / 2;
				sensorData.depthImageBuffer = KinectInterop.CreateComputeBuffer( sensorData.depthImageBuffer, depthBufferLength, sizeof(uint)) ;
			}
			if( sensorData.depthImageBuffer != null && sensorData.depthImage != null ){
				sensorData.depthImageBuffer.SetData( sensorData.depthImage );
			}

			float minDepthDistance = kinectManager.GetSensorMinDistance( _sensorId );
			float maxDepthDistance = kinectManager.GetSensorMaxDistance( _sensorId );

			_renderDepthTextureMaterial.SetInt( ShaderIDs._TexResX, w );
			_renderDepthTextureMaterial.SetInt( ShaderIDs._TexResY, h );
			_renderDepthTextureMaterial.SetInt( ShaderIDs._MinDepth, (int)(minDepthDistance * 1000f) );
			_renderDepthTextureMaterial.SetInt( ShaderIDs._MaxDepth, (int)(maxDepthDistance * 1000f) );
			_renderDepthTextureMaterial.SetBuffer( ShaderIDs._DepthMap, sensorData.depthImageBuffer );
			Graphics.Blit( null, _depthSourceTexture, _renderDepthTextureMaterial );

			if( process ){
				if( !_processedTexture ){
					_processedTexture = new RenderTexture( _depthSourceTexture.width, _depthSourceTexture.height, 0, _depthSourceTexture.graphicsFormat );
					_processedTexture.name = "KinectDepthProcessed";
				}
			}

			if( _undistort ) EnsureUndistortResources( sensorData.depthCamIntr );

			if( process ) Process( _depthSourceTexture );

			_latestTexture = process ? _processedTexture : _depthSourceTexture;
			_previousFrameTimeMicroSeconds = _latestFrameTimeMicroSeconds;
			_latestFrameTimeMicroSeconds = sensorData.lastDepthFrameTime;

			return true;
		}


		void Process( Texture sourceTexture )
		{
			if( _undistort ) {
				_lensUndistorter.Undistort( sourceTexture, _processedTexture, isTextureAxisYFlipped: true ); // We know that the input texture from AxureKinectExample is flipped to begin with.
				if( _flipVertically ) flipper.FlipVertically( _processedTexture );

			} else if( _flipVertically ) {
				flipper.FlipVertically( sourceTexture, _processedTexture );
			}
		}


		void EnsureUndistortResources( KinectInterop.CameraIntrinsics rfilkovIntrinsics )
		{
			if( _lensUndistorter == null ){
				Intrinsics intrinsics = new Intrinsics();
				intrinsics.UpdateFromAzureKinectExamples( rfilkovIntrinsics );
				_lensUndistorter = new LensUndistorter( intrinsics );
			}
		}


		void OnDestroy()
		{
			_processedTexture?.Release();
			_depthSourceTexture?.Release();
			_lensUndistorter?.Release();
			flipper?.Release();
			
			if( _infraredSourceTexture ) Destroy( _infraredSourceTexture );
			if( _renderDepthTextureMaterial ) Destroy( _renderDepthTextureMaterial );
		}
	}
}