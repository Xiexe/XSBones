Shader "Xiexe/XSBoner/IKLines" {
    Properties {

       	_Color ("Color", Color) = (1,1,0,1)

		[Header(Stencil)]
		_Stencil ("Stencil ID [0;255]", Float) = 0
    }
    SubShader {
			Tags { "RenderType"="Transparent" "Queue"="Transparent+1"}
			LOD 100
			Cull Off
			
			Pass{ 

			Ztest Off
			Blend SrcAlpha OneMinusSrcAlpha

			Stencil
			{
				Ref [_Stencil]
				ReadMask 255
				WriteMask 255
				Comp Equal
				Pass Replace

			}

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom
			#define UNITY_PASS_FORWARDBASE
			#pragma multi_compile_fwdbase_fullshadows
			#pragma only_renderers d3d11 glcore gles
			#pragma target 4.0
			
			struct VertexInput {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 color: COLOR;
			};

			struct VertexOutput {
				float4 pos : SV_POSITION;
				float3 normal : NORMAL;
				float4 color: texcoord1;
			};

			struct g2f
			{
				float4 pos : SV_POSITION;
				float4 color: texcoord1;
				float3 normal : TEXCOORD2; 
			};

			float4 _Color;
			VertexOutput vert(VertexInput v) {
				VertexOutput OUT;
				OUT.pos = v.vertex;
				OUT.normal = mul(unity_ObjectToWorld, v.normal);
				OUT.color = _Color;
				return OUT;
			}

			[maxvertexcount(3)]
			void geom(triangle VertexInput IN[3], uint pid : SV_PrimitiveID, inout LineStream<g2f> tristream)
			{
				g2f o;
				//tristream.RestartStrip();
				for (int i = 0; i < 2; i++)
				{
					o.pos = UnityObjectToClipPos(IN[i].vertex);
					o.color = IN[i].color;
					o.normal = IN[i].normal;
					tristream.Append(o);
				}
			}


			float4 frag(g2f i) : COLOR{

				float3 col = i.color;


				return fixed4(col, 1);
			}
				ENDCG
			}

		//OUTOFMESHPASS
			Pass{ 

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom
			#define UNITY_PASS_FORWARDBASE
			#pragma multi_compile_fwdbase_fullshadows
			#pragma only_renderers d3d11 glcore gles
			#pragma target 4.0
			
			struct VertexInput {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 color: COLOR;
			};

			struct VertexOutput {
				float4 pos : SV_POSITION;
				float3 normal : NORMAL;
				float4 color: texcoord1;
			};

			struct g2f
			{
				float4 pos : SV_POSITION;
				float4 color: texcoord1;
				float3 normal : TEXCOORD2; 
			};

			float4 _Color;
			VertexOutput vert(VertexInput v) {
				VertexOutput OUT;
				OUT.pos = v.vertex;
				OUT.normal = mul(unity_ObjectToWorld, v.normal);
				OUT.color = _Color;
				return OUT;
			}

			[maxvertexcount(3)]
			void geom(triangle VertexInput IN[3], uint pid : SV_PrimitiveID, inout LineStream<g2f> tristream)
			{
				g2f o;
				//tristream.RestartStrip();
				for (int i = 0; i < 2; i++)
				{
					o.pos = UnityObjectToClipPos(IN[i].vertex);
					o.color = IN[i].color;
					o.normal = IN[i].normal;
					tristream.Append(o);
				}
			}


			float4 frag(g2f i) : COLOR{

				float3 col = i.color;


				return fixed4(col, 1);
			}
				ENDCG
			}
    }
    FallBack "Diffuse"
}
