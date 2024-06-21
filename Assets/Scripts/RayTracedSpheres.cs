using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static UnityEngine.GraphicsBuffer;

struct Sphere
{
    public Vector3 position;
    public float radius;
    public Vector3 albedo;
    public Vector3 specular;
}

public class RayTracedSpheres : MonoBehaviour
{
    #region parameters
    //Shader to Use for rendering
    public ComputeShader rayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    [Header("Sphere Generation Parameters")]
    public float maxDistance;   //How far from the origin spheres can spawn
    public Vector2 radiusRange; //How large the spheres can be
    public uint MaxNumSpheres;  //How many spheres can spawn at once

    [Header("Ray Tracing Parameters")]
    public float threadRenderSize = 8.0f;
    public int numBounces;

    [Header("Anti-Aliasing Parameters")]
    public float aliasingStrength = 5f;
    
    //Texture to render final result to
    private RenderTexture _target;

    //Reference to the camera we are rendering to
    private Camera _camera;

    private uint _currentSample = 0;
    private Material _addMaterial;

    private ComputeBuffer _sphereBuffer;

    #endregion

    #region Unity Functions
    //Awake is called before start
    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        _currentSample = 0;
        CreateScene();
    }

    private void OnDisable()
    {
        if(_sphereBuffer != null )
            _sphereBuffer.Release();
    }

    // Update is called once per frame
    void Update()
    {
        if(transform.hasChanged || DirectionalLight.transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
            DirectionalLight.transform.hasChanged = false;
        }
    }
    #endregion

    #region Rendering
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    //Controls rendering our result
    private void Render(RenderTexture destination)
    {
        //Make sure we have a render texture initialized
        InitRenderTexture();

        //Set the texture to output to
        rayTracingShader.SetTexture(0, "Result", _target);

        //calculate how many thread groups to use
        int threadGroupsX = Mathf.CeilToInt(Screen.width / threadRenderSize);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / threadRenderSize);

        //Dispatch the threads
        rayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        //Make sure we have the Add Shader initialized
        if (_addMaterial == null)
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));

        _addMaterial.SetFloat("_Sample", _currentSample);

        //Blit the results to the screen
        Graphics.Blit(_target, destination, _addMaterial);

        _currentSample++;
    }

    private void InitRenderTexture()
    {
        //If the target texture isnt valid, make a new one
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release render texture if we already have one
            if (_target != null)
                _target.Release();

            // Get a render target for Ray Tracing
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }
    #endregion

    private void SetShaderParameters()
    {
        //Sets reference to the Camera to World matrix for our camera
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        //Sets reference to the Inverse Projection matrix for our camera
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        //Set reference to our skybox texture
        rayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        //Set the random offset for this render pass
        rayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value) * aliasingStrength);
        //Set the directional light
        Vector3 l = DirectionalLight.transform.forward;
        rayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        //Set spheres in scene
        rayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
    }

    void CreateScene()
    {
        //Initialize random spheres in the scene
        Sphere[] spheres = GenerateRandomSpheres();

        //Send the sphere data to a compute shader
        _sphereBuffer = new ComputeBuffer(spheres.Length, sizeof(float)*10);
        _sphereBuffer.SetData(spheres);
    }

    Sphere[] GenerateRandomSpheres()
    {
        List<Sphere> spheres = new List<Sphere>();

        for(int i = 0; i < MaxNumSpheres; i++)
        {
            //Place the sphere
            Sphere temp = new Sphere();
            
            temp.radius = Random.Range(radiusRange.x, radiusRange.y);
            var randPos = Random.insideUnitCircle * maxDistance;
            temp.position = new Vector3(randPos.x, temp.radius, randPos.y);

            //Reject it if it overlaps
            foreach (Sphere other in spheres)
            {
                if(other.radius + temp.radius > (other.position - temp.position).magnitude)
                {
                    goto SkipSphere;
                }
            }

            // Albedo and specular color
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            temp.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            temp.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            spheres.Add(temp);

        SkipSphere:
            continue;
        }

        return spheres.ToArray();
    }
}
