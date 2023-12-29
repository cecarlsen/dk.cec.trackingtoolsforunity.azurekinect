/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Events;
using com.rfilkov.kinect;
using UnityEngine.Experimental.Rendering;
using System;

namespace TrackingTools.AzureKinect
{
	public class KinectAzureTextureProvider : MonoBehaviour
	{
		[SerializeField] int _sensorId = 0;

		[Header("State")]
		public bool colorEnabled = true;
		public bool irEnabled = true;
		public bool depthEnabled = true;

		[Header("Processing")]
		[SerializeField] bool _flipColor = false;
		[SerializeField] bool _undistortIR = false;
		[SerializeField] bool _flipIR = false;
		[SerializeField] bool _undistortDepth = false;
		[SerializeField] bool _flipDepth = false;

		[Header("Output")]
		[SerializeField] UnityEvent<Texture> _colorTextureEvent = new UnityEvent<Texture>();
		[SerializeField] UnityEvent<Texture> _irTextureEvent = new UnityEvent<Texture>();
		[SerializeField] UnityEvent<Texture> _depthTextureEvent = new UnityEvent<Texture>();

		ulong _latestIRFrameTime;
		ulong _latestColorFrameTime;
		ulong _latestDepthFrameTime;

		bool _wasColorUpdatedThisFrame;
		bool _wasIRUpdatedThisFrame;
		bool _wasDepthUpdatedThisFrame;

		Material _renderDepthTextureMaterial;

		RenderTexture _processedColorTexture;
		RenderTexture _processedIRTexture;
		RenderTexture _depthTexture, _processedDepthTexture;

		Texture _latestColorTexture;
		Texture _latestIRTexture;
		Texture _latestDepthTexture;

		Texture2D _irTexture;
		byte[] _rawImageDataBytes;

		LensUndistorter _depthLensUndistorter;
		Flipper _flipper;

		public bool wasColorUpdatedThisFrame => _wasColorUpdatedThisFrame;
		public bool wasIRUpdatedThisFrame => _wasIRUpdatedThisFrame;
		public bool wasDepthUpdatedThisFrame => _wasDepthUpdatedThisFrame;


		static class ShaderIDs
		{
			public static readonly int _DistMapTex = Shader.PropertyToID( nameof( _DistMapTex ) );
			public static readonly int _TexResX = Shader.PropertyToID( nameof( _TexResX ) );
			public static readonly int _TexResY = Shader.PropertyToID( nameof( _TexResY ) );
			public static readonly int _MinDepth = Shader.PropertyToID( nameof( _MinDepth ) );
			public static readonly int _MaxDepth = Shader.PropertyToID( nameof( _MaxDepth ) );
			public static readonly int _DepthMap = Shader.PropertyToID( nameof( _DepthMap ) );
		}


		public Texture GetLatestColorTexture()
		{
			if( !colorEnabled ) throw new Exception( "Color was requested but not enabled" );

			return _latestColorTexture;
		}


		public Texture GetLatestIRTexture()
		{
			if( !irEnabled ) throw new Exception( "Infrared was requested but not enabled" );

			return _latestIRTexture;
		}


		public Texture GetLatestDepthTexture()
		{
			if( !depthEnabled ) throw new Exception( "Infrared was requested but not enabled" );

			return _latestDepthTexture;
		}


		void Awake()
		{
			Shader shader = Shader.Find( "KinectAzureTextureProvider/KinectDepthShader" );
			_renderDepthTextureMaterial = new Material( shader );

			_flipper = new Flipper();

			KinectManager kinectManager = KinectManager.Instance;
			if( !kinectManager ) return;
		}


		void Update()
		{
			KinectManager kinectManager = KinectManager.Instance;
			if( !kinectManager || !kinectManager.IsInitialized() ) return;

			if( colorEnabled && kinectManager.getColorFrames == KinectManager.ColorTextureType.None ) kinectManager.getColorFrames = KinectManager.ColorTextureType.ColorTexture;
			if( irEnabled && kinectManager.getInfraredFrames != KinectManager.InfraredTextureType.None ) kinectManager.getInfraredFrames = KinectManager.InfraredTextureType.RawInfraredData;
			if( depthEnabled && kinectManager.getDepthFrames == KinectManager.DepthTextureType.None ) kinectManager.getDepthFrames = KinectManager.DepthTextureType.RawDepthData;

			KinectInterop.SensorData sensorData = kinectManager.GetSensorData( _sensorId );

			_wasColorUpdatedThisFrame = false;
			_wasIRUpdatedThisFrame = false;
			_wasDepthUpdatedThisFrame = false;

			if( colorEnabled ) UpdateColorTexture( kinectManager, sensorData );
			if( irEnabled ) UpdateIRTexture( kinectManager, sensorData );
			if( depthEnabled ) UpdateDepthTexture( kinectManager, sensorData );
		}


		void UpdateColorTexture( KinectManager kinectManager, KinectInterop.SensorData sensorData )
		{
			if( sensorData.lastColorFrameTime == _latestColorFrameTime ) return;

			Texture colorTexture = kinectManager.GetColorImageTex( _sensorId );
			colorTexture.wrapMode = TextureWrapMode.Repeat;
			if( colorTexture ){
				if( string.IsNullOrEmpty( colorTexture.name ) ) colorTexture.name = "KinectColor";
			}

			if( _flipColor ){
				if( !_processedColorTexture || _processedColorTexture.width != colorTexture.width || _processedColorTexture.height != colorTexture.height ) {
					_processedColorTexture?.Release();
					_processedColorTexture = new RenderTexture( colorTexture.width, colorTexture.height, 0, colorTexture.graphicsFormat );
					_processedColorTexture.name = "KinectColorProcessed";
				}

				_flipper.Flip( colorTexture, _processedColorTexture );
			}

			_latestColorTexture = _flipColor ? _processedColorTexture : colorTexture;
			_colorTextureEvent.Invoke( _latestColorTexture );

			_latestColorFrameTime = sensorData.lastColorFrameTime;
			_wasColorUpdatedThisFrame = true;
		}


		void UpdateIRTexture( KinectManager kinectManager, KinectInterop.SensorData sensorData )
		{
			if( sensorData.lastInfraredFrameTime == _latestIRFrameTime ) return;

			// Azure Kinect Examples loads IR as RGB24, but the IR is actually R16, so we loose a lot of information.
			// If you look IR in the official Azure Kinect Viewver it matches Azure Kinet Examples. They are very bright, and typically burned out.
			// We want the full spectrum, so we load 16bit directly to texture.

			int width = sensorData.depthImageWidth;
			int height = sensorData.depthImageHeight;
				
			// Create texture.
			if( !_irTexture ) {
				int pixelCount = width * height;
				_irTexture = new Texture2D( width, height, GraphicsFormat.R16_UNorm, TextureCreationFlags.None );
				_irTexture.name = "KinectIR";
				_rawImageDataBytes = new byte[ pixelCount * 2 ];
			}

			// Get raw image data.
			ushort[] rawImageData = kinectManager.GetRawInfraredMap( _sensorId );

			// ushort[] to byte[].
			// https://stackoverflow.com/questions/37213819/convert-ushort-into-byte-and-back
			Buffer.BlockCopy( rawImageData, 0, _rawImageDataBytes, 0, rawImageData.Length * 2 );

			// Load into texture.
			_irTexture.LoadRawTextureData( _rawImageDataBytes );
			_irTexture.Apply();

			if( _irTexture && string.IsNullOrEmpty( _irTexture.name ) ) _irTexture.name = "KinectIR";

			if( _undistortIR || _flipIR ){
				if( !_processedIRTexture ){
					_processedIRTexture = new RenderTexture( _irTexture.width, _irTexture.height, 0, _irTexture.graphicsFormat );
					_processedIRTexture.name = "KinectIRProcessed";
				}
			}

			if( _undistortIR ){
				EnsureIRDepthUndistortResources( sensorData.depthCamIntr );
				_depthLensUndistorter.Undistort( _irTexture, _processedIRTexture, isTextureAxisYFlipped: true ); // We know that the input texture from AxureKinectExample is flipped to begin with.
				if( _flipIR ) _flipper.Flip( _processedIRTexture );

			} else if( _flipIR ) {
				_flipper.Flip( _irTexture, _processedIRTexture );
			}

			_latestIRTexture = _undistortIR || _flipIR ? _processedIRTexture : _irTexture;
			_irTextureEvent.Invoke( _latestIRTexture );

			_latestIRFrameTime = sensorData.lastInfraredFrameTime;
			_wasIRUpdatedThisFrame = true;
		}


		void UpdateDepthTexture( KinectManager kinectManager, KinectInterop.SensorData sensorData )
		{
			//Debug.Log( sensorData.startTimeOffset + " " + kinectManager.GetDepthFrameTime( 0 ) + " == " + sensorData.lastDepthFrameTime );

			if( sensorData.lastDepthFrameTime == _latestDepthFrameTime ) return;

			// Don't use AzureKinectExamples depth. It is RGB888 and encodes depth to two-color hue.
			int w = kinectManager.GetDepthImageWidth( _sensorId );
			int h = kinectManager.GetDepthImageHeight( _sensorId );
			if( !_depthTexture || _depthTexture.width != w || _depthTexture.height != h ){
				if( _depthTexture ) _depthTexture.Release();
				_depthTexture = new RenderTexture( w, h, 0, RenderTextureFormat.RFloat );
				_depthTexture.name = "KinectDepth";
			}

			if(sensorData.depthImageBuffer == null)
			{
				int depthBufferLength = sensorData.depthImageWidth * sensorData.depthImageHeight / 2;
				sensorData.depthImageBuffer = KinectInterop.CreateComputeBuffer(sensorData.depthImageBuffer, depthBufferLength, sizeof(uint));
			}

			if ( sensorData.depthImageBuffer != null && sensorData.depthImage != null )
			{
				//int depthBufferLength = sensorData.depthImageWidth * sensorData.depthImageHeight / 2;
				//KinectInterop.SetComputeBufferData( sensorData.depthImageBuffer, sensorData.depthImage, depthBufferLength, sizeof(uint));
				sensorData.depthImageBuffer.SetData( sensorData.depthImage );
			}

			float minDepthDistance = kinectManager.GetSensorMinDistance( _sensorId );
			float maxDepthDistance = kinectManager.GetSensorMaxDistance( _sensorId );

			_renderDepthTextureMaterial.SetInt( ShaderIDs._TexResX, w );
			_renderDepthTextureMaterial.SetInt( ShaderIDs._TexResY, h );
			_renderDepthTextureMaterial.SetInt( ShaderIDs._MinDepth, (int)(minDepthDistance * 1000f) );
			_renderDepthTextureMaterial.SetInt( ShaderIDs._MaxDepth, (int)(maxDepthDistance * 1000f) );
			_renderDepthTextureMaterial.SetBuffer( ShaderIDs._DepthMap, sensorData.depthImageBuffer );
			Graphics.Blit( null, _depthTexture, _renderDepthTextureMaterial );

			if( _undistortDepth || _flipDepth ){
				if( !_processedDepthTexture ){
					_processedDepthTexture = new RenderTexture( _depthTexture.width, _depthTexture.height, 0, _depthTexture.graphicsFormat );
					_processedDepthTexture.name = "KinectDepthProcessed";
				}
			}

			_depthTexture.filterMode = _undistortDepth ? FilterMode.Point : FilterMode.Bilinear; // Don't interpolate depth values.
			if( _undistortDepth ){
				EnsureIRDepthUndistortResources( sensorData.depthCamIntr );
				_depthLensUndistorter.Undistort( _depthTexture, _processedDepthTexture, isTextureAxisYFlipped: true ); // We know that the input texture from AxureKinectExample is flipped to begin with.
				if( _flipDepth ) _flipper.Flip( _processedDepthTexture );

			} else if( _flipDepth ){
				_flipper.Flip( _depthTexture, _processedDepthTexture );
			}

			_latestDepthTexture = _undistortDepth || _flipDepth ? _processedDepthTexture : _depthTexture;
			_depthTextureEvent.Invoke( _latestDepthTexture );

			_latestDepthFrameTime = sensorData.lastDepthFrameTime;
			_wasDepthUpdatedThisFrame = true;
		}


		void EnsureIRDepthUndistortResources( KinectInterop.CameraIntrinsics rfilkovIntrinsics )
		{
			if( _depthLensUndistorter == null ){
				Intrinsics intrinsics = new Intrinsics();
				intrinsics.UpdateFromAzureKinectExamples( rfilkovIntrinsics );
				_depthLensUndistorter = new LensUndistorter( intrinsics );
			}
		}


		void OnDestroy()
		{
			_processedColorTexture?.Release();
			_depthTexture?.Release();
			_depthLensUndistorter?.Release();
			_processedIRTexture?.Release();
			_flipper?.Release();
			
			if( _irTexture ) Destroy( _irTexture );
			if( _renderDepthTextureMaterial ) Destroy( _renderDepthTextureMaterial );
		}
	}
}