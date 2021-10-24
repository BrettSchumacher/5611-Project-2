using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stars : MonoBehaviour
{
    public GameObject starPrefab;
    public float dist;
    public int num = 1000;
    [Range(0f, 1f)]
    public float radius_rand = 0.5f;
    public Gradient starColor;

    private List<GameObject> stars;

    // Start is called before the first frame update
    void Start()
    {
        GenerateStars();
    }

    public void GenerateStars()
    {
        stars = new List<GameObject>();
        GameObject temp;
        float scale;

        for (int i = 0; i < num; i++)
        {
            temp = Instantiate(starPrefab);
            temp.transform.position = Random.onUnitSphere * dist;
            temp.transform.parent = transform;
            scale = Random.Range(1f - radius_rand, 1 + radius_rand);
            temp.transform.localScale *= scale;
            temp.GetComponent<MeshRenderer>().material.color = starColor.Evaluate(Random.value);
            stars.Add(temp);
        }
    }
}
