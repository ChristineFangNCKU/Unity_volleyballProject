using UnityEngine;

/// <summary>
/// 自動生成排球場的腳本。
/// 將此腳本附加到一個空的 GameObject 上，並在 Inspector 中設定參數。
/// </summary>
public class CourtGenerator : MonoBehaviour
{
    [Header("場地尺寸 (公尺)")]
    [SerializeField] private float courtWidth = 9.0f;
    [SerializeField] private float courtLength = 18.0f;
    [SerializeField] private float freeZoneWidth = 3.0f;

    [Header("球網與球柱參數")]
    [SerializeField] private float netHeight = 2.43f; // 男子網高
    [SerializeField] private float postHeight = 2.55f;
    [SerializeField] private float postDiameter = 0.1f;
    [SerializeField] private float postDistanceFromCourt = 1.0f;

    [Header("標誌竿參數")]
    [SerializeField] private float antennaHeight = 1.8f;
    [SerializeField] private float antennaDiameter = 0.01f;

    [Header("材質設定")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material linesMaterial; // 需要一張 2:1 的透明線條貼圖
    [SerializeField] private Material postMaterial;
    [SerializeField] private Material netMaterial;
    [SerializeField] private Material antennaMaterial;

    // 按鈕，用於在編輯器模式下觸發生成
    [ContextMenu("Generate Volleyball Court")]
    public void GenerateCourt()
    {
        // --- 為了方便重新生成，先清除所有舊的子物件 ---
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        // --- 建立場地與自由區 ---
        CreateFloor();

        // --- 建立標誌線 ---
        CreateLines();

        // --- 建立球柱 ---
        CreatePosts();

        // --- 建立球網 ---
        CreateNet();

        // --- 建立標誌竿 ---
        CreateAntennas();
    }

    private void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "CourtFloor";
        floor.transform.SetParent(this.transform);
        
        // Plane 預設是 10x10 公尺
        float totalWidth = courtWidth + 2 * freeZoneWidth;
        float totalLength = courtLength + 2 * freeZoneWidth;
        
        // 設定位置：原點在球場左下角，所以整個場地的中心點在 (寬/2, 長/2)
        floor.transform.position = new Vector3(courtWidth / 2, 0, courtLength / 2);
        floor.transform.localScale = new Vector3(totalWidth / 10.0f, 1.0f, totalLength / 10.0f);
        
        floor.GetComponent<Renderer>().material = floorMaterial;
    }

    private void CreateLines()
    {
        GameObject lines = GameObject.CreatePrimitive(PrimitiveType.Plane);
        lines.name = "CourtLines";
        lines.transform.SetParent(this.transform);

        // 設定位置：中心點在 (寬/2, 長/2)，Y軸稍微抬高以避免與地板重疊閃爍
        lines.transform.position = new Vector3(courtWidth / 2, 0.01f, courtLength / 2);
        lines.transform.localScale = new Vector3(courtWidth / 10.0f, 1.0f, courtLength / 10.0f);
        
        // 設定透明材質
        Renderer linesRenderer = lines.GetComponent<Renderer>();
        linesRenderer.material = linesMaterial;
    }

    private void CreatePosts()
    {
        // 預設 Cylinder 高度為 2，直徑為 1
        float cylinderDefaultHeight = 2.0f;

        // 左球柱
        GameObject postLeft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        postLeft.name = "Post_Left";
        postLeft.transform.SetParent(this.transform);
        // 位置：X 在場外，Y 在柱子高度一半，Z 在中線
        postLeft.transform.position = new Vector3(-postDistanceFromCourt, postHeight / 2, courtLength / 2);
        postLeft.transform.localScale = new Vector3(postDiameter, postHeight / cylinderDefaultHeight, postDiameter);
        postLeft.GetComponent<Renderer>().material = postMaterial;

        // 右球柱
        GameObject postRight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        postRight.name = "Post_Right";
        postRight.transform.SetParent(this.transform);
        postRight.transform.position = new Vector3(courtWidth + postDistanceFromCourt, postHeight / 2, courtLength / 2);
        postRight.transform.localScale = new Vector3(postDiameter, postHeight / cylinderDefaultHeight, postDiameter);
        postRight.GetComponent<Renderer>().material = postMaterial;
    }

    private void CreateNet()
    {
        // 使用 Cube 來製作球網，可以給予一點厚度
        GameObject net = GameObject.CreatePrimitive(PrimitiveType.Cube);
        net.name = "Net";
        net.transform.SetParent(this.transform);

        float netVisualWidth = 1.0f; // 球網上下寬度
        float netThickness = 0.02f;
        
        // 位置：X 在場地中線，Y 在網高中心，Z 在中線
        net.transform.position = new Vector3(courtWidth / 2, netHeight - (netVisualWidth / 2), courtLength / 2);
        net.transform.localScale = new Vector3(courtWidth, netVisualWidth, netThickness);
        net.GetComponent<Renderer>().material = netMaterial;
    }
    
    private void CreateAntennas()
    {
        // 預設 Cylinder 高度為 2
        float cylinderDefaultHeight = 2.0f;
        float netVisualWidth = 1.0f; // 球網寬度
        
        // 標誌竿有 1m 在網內，0.8m 在網外，總長 1.8m
        // 它的底部在球網頂部減去球網寬度的地方
        float antennaBaseY = netHeight - netVisualWidth;
        float antennaCenterY = antennaBaseY + (antennaHeight / 2);

        // 左標誌竿
        GameObject antennaLeft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        antennaLeft.name = "Antenna_Left";
        antennaLeft.transform.SetParent(this.transform);
        // 位置：X 在左邊線 (0)，Y 在標誌竿中心，Z 在中線
        antennaLeft.transform.position = new Vector3(0, antennaCenterY, courtLength / 2);
        antennaLeft.transform.localScale = new Vector3(antennaDiameter, antennaHeight / cylinderDefaultHeight, antennaDiameter);
        antennaLeft.GetComponent<Renderer>().material = antennaMaterial;

        // 右標誌竿
        GameObject antennaRight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        antennaRight.name = "Antenna_Right";
        antennaRight.transform.SetParent(this.transform);
        // 位置：X 在右邊線 (courtWidth)，Y 在標誌竿中心，Z 在中線
        antennaRight.transform.position = new Vector3(courtWidth, antennaCenterY, courtLength / 2);
        antennaRight.transform.localScale = new Vector3(antennaDiameter, antennaHeight / cylinderDefaultHeight, antennaDiameter);
        antennaRight.GetComponent<Renderer>().material = antennaMaterial;
    }
}
