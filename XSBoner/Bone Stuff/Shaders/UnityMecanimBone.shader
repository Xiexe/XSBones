Shader "Xiexe/XSBoner/UnityMecanim"
{
	Properties
	{
		[Header(Global Wire Settings)]
		_WireThickness ("Wire Thickness", RANGE(0, 800)) = 300
		_WireSmoothness ("Wire Smoothness", RANGE(0, 20)) = 3
		//_MaxTriSize ("Max Tri Size", RANGE(0, 200)) = 25
		
		//144 = .56471
		//96 = .37647

		[Header(Humanoid Bone Colors)]
		_WireColor ("Wire Color", Color) = (0, 1, .56471, 1)
		_BaseColor ("Base Color", Color) = (0, 1, .56471, .37647)

		[Header(NonHumanoid Bone Colors)]
		_WireColor2 ("Wire Color", Color) = (.55, .55, .55, 1)
		_BaseColor2 ("Base Color", Color) = (.55, .55, .55, .37647)

		[Header(Dynamic Bone Colors)]
		_WireColor3 ("Wire Color", Color) = (0, .56471, 1, 1)
		_BaseColor3 ("Base Color", Color) = (0, .56471, 1, .37647)

		[Header(Stencil)]
		// _Offset("Offset", float) = 0
		_Stencil ("Stencil ID [0;255]", Float) = 0
		// _ReadMask ("ReadMask [0;255]", Int) = 255
		// _WriteMask ("WriteMask [0;255]", Int) = 255
		// [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Int) = 8
		// [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Int) = 2
		// [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail", Int) = 0
		// [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail", Int) = 0
		// [Enum(Off,0,On,1)] _ZWrite("ZWrite", Int) = 0
		// [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Int) = 0
		// [Enum(None,0,Alpha,1,Red,8,Green,4,Blue,2,RGB,14,RGBA,15)] _colormask("Color Mask", Int) = 15 
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent"}
		LOD 100

		Blend SrcAlpha OneMinusSrcAlpha // blend


		Pass
		{

			ZTest Always
			ZWrite On

			Stencil
			{
				Ref [_Stencil]
				ReadMask 255
				WriteMask 255
				Comp Equal
				Pass Keep
			}


			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			float _WireThickness;
			float _WireSmoothness;
			float4 _WireColor;
			float4 _BaseColor;
			float _MaxTriSize;
			float4 _WireColor2;
			float4 _BaseColor2;
			float4 _WireColor3;
			float4 _BaseColor3;

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2g
			{
				float4 projectionSpaceVertex : SV_POSITION;
				float4 worldSpacePosition : TEXCOORD1;
				float4 color : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			struct g2f
			{
				float4 projectionSpaceVertex : SV_POSITION;
				float4 worldSpacePosition : TEXCOORD0;
				float4 dist : TEXCOORD1;
				float4 area : TEXCOORD2;
				float4 color : TEXCOORD3;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2g vert (appdata v)
			{
				v2g o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.projectionSpaceVertex = UnityObjectToClipPos(v.vertex);
				o.worldSpacePosition = mul(unity_ObjectToWorld, v.vertex);
				o.color = v.color;
				return o;
			}

			[maxvertexcount(3)]
			void geom(triangle v2g i[3], inout TriangleStream<g2f> triangleStream)
			{
				float2 p0 = i[0].projectionSpaceVertex.xy / i[0].projectionSpaceVertex.w;
				float2 p1 = i[1].projectionSpaceVertex.xy / i[1].projectionSpaceVertex.w;
				float2 p2 = i[2].projectionSpaceVertex.xy / i[2].projectionSpaceVertex.w;

				float2 edge0 = p2 - p1;
				float2 edge1 = p2 - p0;
				float2 edge2 = p1 - p0;

				float4 worldEdge0 = i[0].worldSpacePosition - i[1].worldSpacePosition;
				float4 worldEdge1 = i[1].worldSpacePosition - i[2].worldSpacePosition;
				float4 worldEdge2 = i[0].worldSpacePosition - i[2].worldSpacePosition;

				// To find the distance to the opposite edge, we take the
				// formula for finding the area of a triangle Area = Base/2 * Height, 
				// and solve for the Height = (Area * 2)/Base.
				// We can get the area of a triangle by taking its cross product
				// divided by 2.  However we can avoid dividing our area/base by 2
				// since our cross product will already be double our area.
				float area = abs(edge1.x * edge2.y - edge1.y * edge2.x);
				float wireThickness = 800 - _WireThickness;

				g2f o;

				o.area = float4(0, 0, 0, 0);
				o.area.x = max(length(worldEdge0), max(length(worldEdge1), length(worldEdge2)));

				o.worldSpacePosition = i[0].worldSpacePosition;
				o.projectionSpaceVertex = i[0].projectionSpaceVertex;
				o.dist.xyz = float3( (area / length(edge0)), 0.0, 0.0) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
								o.color = i[0].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[0], o);
				triangleStream.Append(o);

				o.worldSpacePosition = i[1].worldSpacePosition;
				o.projectionSpaceVertex = i[1].projectionSpaceVertex;
				o.dist.xyz = float3(0.0, (area / length(edge1)), 0.0) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
								o.color = i[1].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[1], o);
				triangleStream.Append(o);

				o.worldSpacePosition = i[2].worldSpacePosition;
				o.projectionSpaceVertex = i[2].projectionSpaceVertex;
				o.dist.xyz = float3(0.0, 0.0, (area / length(edge2))) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
								o.color = i[2].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[2], o);
				triangleStream.Append(o);
			}

			fixed4 frag(g2f i) : SV_Target
			{
				float minDistanceToEdge = min(i.dist[0], min(i.dist[1], i.dist[2])) * i.dist[3];

				if(minDistanceToEdge > 0.9 || i.area.x > 25)
					{
						float4 basecolors1 = lerp(_BaseColor2, _BaseColor, i.color.r);
						float4 basecolors2 = lerp(basecolors1, _BaseColor3, i.color.b);
						
						return basecolors2;
					}

			// Smooth our line out
				float t = exp2(_WireSmoothness * -1.0 * minDistanceToEdge * minDistanceToEdge);
			//Humanoid	
				fixed4 finalColorH = lerp(_BaseColor, _WireColor, t);
			//NonHumanoid
				fixed4 finalColorNH = lerp(_BaseColor2, _WireColor2, t);
			//Dynamic Bone
				fixed4 finalColorDB = lerp(_BaseColor3, _WireColor3, t);

				float4 colors1 = lerp(finalColorNH, finalColorH, i.color.r);
				float4 colors2 = lerp(colors1, finalColorDB, i.color.b);

				return colors2;

			}
			ENDCG
		}

		// SECOND PASS FOR BONES OUTSIDE OF THE MESH, FIRST PASS MUST BE STENCILED IN.
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			float _WireThickness;
			float _WireSmoothness;
			float4 _WireColor;
			float4 _BaseColor;
			float _MaxTriSize;
			float4 _WireColor2;
			float4 _BaseColor2;
			float4 _WireColor3;
			float4 _BaseColor3;

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2g
			{
				float4 projectionSpaceVertex : SV_POSITION;
				float4 worldSpacePosition : TEXCOORD1;
				float4 color : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			struct g2f
			{
				float4 projectionSpaceVertex : SV_POSITION;
				float4 worldSpacePosition : TEXCOORD0;
				float4 dist : TEXCOORD1;
				float4 area : TEXCOORD2;
				float4 color : TEXCOORD3;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2g vert (appdata v)
			{
				v2g o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.projectionSpaceVertex = UnityObjectToClipPos(v.vertex);
				o.worldSpacePosition = mul(unity_ObjectToWorld, v.vertex);
				o.color = v.color;
				return o;
			}

			[maxvertexcount(3)]
			void geom(triangle v2g i[3], inout TriangleStream<g2f> triangleStream)
			{
				float2 p0 = i[0].projectionSpaceVertex.xy / i[0].projectionSpaceVertex.w;
				float2 p1 = i[1].projectionSpaceVertex.xy / i[1].projectionSpaceVertex.w;
				float2 p2 = i[2].projectionSpaceVertex.xy / i[2].projectionSpaceVertex.w;

				float2 edge0 = p2 - p1;
				float2 edge1 = p2 - p0;
				float2 edge2 = p1 - p0;

				float4 worldEdge0 = i[0].worldSpacePosition - i[1].worldSpacePosition;
				float4 worldEdge1 = i[1].worldSpacePosition - i[2].worldSpacePosition;
				float4 worldEdge2 = i[0].worldSpacePosition - i[2].worldSpacePosition;

				// To find the distance to the opposite edge, we take the
				// formula for finding the area of a triangle Area = Base/2 * Height, 
				// and solve for the Height = (Area * 2)/Base.
				// We can get the area of a triangle by taking its cross product
				// divided by 2.  However we can avoid dividing our area/base by 2
				// since our cross product will already be double our area.
				float area = abs(edge1.x * edge2.y - edge1.y * edge2.x);
				float wireThickness = 800 - _WireThickness;

				g2f o;

				o.area = float4(0, 0, 0, 0);
				o.area.x = max(length(worldEdge0), max(length(worldEdge1), length(worldEdge2)));

				o.worldSpacePosition = i[0].worldSpacePosition;
				o.projectionSpaceVertex = i[0].projectionSpaceVertex;
				o.dist.xyz = float3( (area / length(edge0)), 0.0, 0.0) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
								o.color = i[0].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[0], o);
				triangleStream.Append(o);

				o.worldSpacePosition = i[1].worldSpacePosition;
				o.projectionSpaceVertex = i[1].projectionSpaceVertex;
				o.dist.xyz = float3(0.0, (area / length(edge1)), 0.0) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
								o.color = i[1].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[1], o);
				triangleStream.Append(o);

				o.worldSpacePosition = i[2].worldSpacePosition;
				o.projectionSpaceVertex = i[2].projectionSpaceVertex;
				o.dist.xyz = float3(0.0, 0.0, (area / length(edge2))) * o.projectionSpaceVertex.w * wireThickness;
				o.dist.w = 1.0 / o.projectionSpaceVertex.w;
								o.color = i[2].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[2], o);
				triangleStream.Append(o);
			}

			fixed4 frag(g2f i) : SV_Target
			{
				float minDistanceToEdge = min(i.dist[0], min(i.dist[1], i.dist[2])) * i.dist[3];

				if(minDistanceToEdge > 0.9 || i.area.x > 25)
					{
						float4 basecolors1 = lerp(_BaseColor2, _BaseColor, i.color.r);
						float4 basecolors2 = lerp(basecolors1, _BaseColor3, i.color.b);
						
						return basecolors2;
					}

			// Smooth our line out
				float t = exp2(_WireSmoothness * -1.0 * minDistanceToEdge * minDistanceToEdge);
			//Humanoid	
				fixed4 finalColorH = lerp(_BaseColor, _WireColor, t);
			//NonHumanoid
				fixed4 finalColorNH = lerp(_BaseColor2, _WireColor2, t);
			//Dynamic Bone
				fixed4 finalColorDB = lerp(_BaseColor3, _WireColor3, t);

				float4 colors1 = lerp(finalColorNH, finalColorH, i.color.r);
				float4 colors2 = lerp(colors1, finalColorDB, i.color.b);

				return colors2;

			}
			ENDCG
		}


	}
}
