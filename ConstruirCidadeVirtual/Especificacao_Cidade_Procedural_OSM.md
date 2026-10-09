# Especificação Técnica Avançada: Gerador Procedural de Cidades (OSM) para Unity

## 1. Objetivo do Prompt
Desenvolver um sistema modular avançado em C# para o Unity Game Engine que faça o parse de um arquivo `.osm` (XML) e gere proceduralmente uma cidade tridimensional completa. O sistema deve criar a geometria detalhada de prédios (com cores, andares e janelas repetíveis), estradas asfaltadas e calçadas com textura de petit-pavé (mosaico português), tudo mapeado corretamente em coordenadas UV e otimizado para performance.

---

## 2. Requisitos de Geração de Elementos Urbanos

### A. Prédios 3D Detalhados (Cores, Alturas e Janelas)
*   **Geometria das Paredes:** Cada segmento de parede deve ser dividido horizontalmente em andares (baseado na tag `building:levels`) e verticalmente em seções de largura fixa (ex: 3 a 4 metros) para criar quads individuais para cada "janela".
*   **Mapeamento UV de Janelas:** Em vez de modelar janelas com geometria complexa, aplique coordenadas UV específicas em cada quad de parede criado. As UVs devem mapear para uma textura de *atlas* ou *trim sheet* contendo molduras e vidros refletores.
*   **Variação de Cores:** O script deve aplicar uma cor procedural aleatória ao material do prédio (via `MaterialPropertyBlock` para evitar quebra de batching) ou ler a tag opcional `building:color` do OSM.
*   **Fechamento do Teto:** Utilizar triangulação por *Ear Clipping* na altura máxima do edifício (`levels * 3.5m`), aplicando UVs planas projetadas de cima para baixo ($X, Z$).

### B. Estradas (Asfalto)
*   **Geração de Malha Extrudada:** Em vez de `LineRenderer`, o sistema deve criar uma malha plana (`Mesh`) real. Para cada segmento da via, calcula-se o vetor perpendicular (direção normal à estrada no plano $XZ$) e estende-se os vértices para a esquerda e para a direita com base na largura da pista (`highway=motorway`: ~10m, `residential`: ~6m).
*   **Mapeamento UV do Asfalto:** As coordenadas UV devem ser contínuas ao longo do comprimento da estrada ($V$ avança com a distância acumulada e $U$ varia de 0 a 1 de uma borda à outra) para permitir texturas repetíveis de asfalto com faixas centrais.

### C. Calçadas (Petit-Pavé / Mosaico Português)
*   **Geração Adjacente:** A calçada deve ser gerada paralelamente às bordas da malha da estrada, deslocada externamente por uma distância padrão (ex: 1.5 a 2 metros).
*   **Elevação (Meio-Fio):** A malha da calçada deve ser elevada verticalmente em $Y = 0.15m$ (15 centímetros) em relação à estrada, criando uma face vertical fechada (meio-fio/cume).
*   **Mapeamento UV de Mosaico:** Aplicar mapeamento UV planar ou contínuo repetível saturado para uma textura de Petit-Pavé, garantindo que o padrão de pedras não fique esticado ou distorcido nas curvas.

---

## 3. Arquitetura do Código em C#

### Script 1: Conversor Geográfico (`GeoConverter.cs`)
```csharp
using UnityEngine;

public static class GeoConverter
{
    private const float EarthRadius = 6378137.0f;

    public static Vector3 LatLonToEarthMeters(float lat, float lon)
    {
        float x = EarthRadius * lon * Mathf.Deg2Rad;
        float z = EarthRadius * Mathf.Log(Mathf.Tan((90.0f + lat) * Mathf.Deg2Rad / 2.0f));
        return new Vector3(x, 0, z);
    }
}
```

### Script 2: Parser e Gerador Avançado (`AdvancedOSMParser.cs`)
```csharp
using System.Xml;
using System.Collections.Generic;
using UnityEngine;

public class AdvancedOSMParser : MonoBehaviour
{
    public string osmFilePath;
    public Material buildingMaterial;
    public Material asphaltMaterial;
    public Material sidewalkMaterial;

    private Dictionary<long, Vector3> nodeMap = new Dictionary<long, Vector3>();
    private Vector3 mapOrigin;
    private bool isOriginSet = false;

    void Start()
    {
        ParseAndGenerate();
    }

    void ParseAndGenerate()
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(osmFilePath);

        // 1. Fase de leitura dos Nodes
        XmlNodeList nodes = doc.GetElementsByTagName("node");
        foreach (XmlNode node in nodes)
        {
            long id = long.Parse(node.Attributes["id"].Value);
            float lat = float.Parse(node.Attributes["lat"].Value);
            float lon = float.Parse(node.Attributes["lon"].Value);

            Vector3 worldMeters = GeoConverter.LatLonToEarthMeters(lat, lon);
            if (!isOriginSet) { mapOrigin = worldMeters; isOriginSet = true; }
            nodeMap.Add(id, worldMeters - mapOrigin);
        }

        // 2. Fase de processamento das Ways
        XmlNodeList ways = doc.GetElementsByTagName("way");
        foreach (XmlNode way in ways)
        {
            List<Vector3> points = new List<Vector3>();
            foreach (XmlNode child in way.ChildNodes)
            {
                if (child.Name == "nd")
                {
                    long nodeRef = long.Parse(child.Attributes["ref"].Value);
                    if (nodeMap.ContainsKey(nodeRef)) points.Add(nodeMap[nodeRef]);
                }
            }

            bool isBuilding = false;
            bool isHighway = false;
            int levels = 3;
            Color buildingColor = Color.white;

            foreach (XmlNode child in way.ChildNodes)
            {
                if (child.Name == "tag")
                {
                    string key = child.Attributes["k"].Value;
                    string val = child.Attributes["v"].Value;

                    if (key == "building") isBuilding = true;
                    if (key == "highway") isHighway = true;
                    if (key == "building:levels") int.TryParse(val, out levels);
                    if (key == "building:color") ColorUtility.TryParseHtmlString(val, out buildingColor);
                }
            }

            if (isBuilding && points.Count > 2)
            {
                GenerateDetailedBuilding(points, levels, buildingColor);
            }
            else if (isHighway && points.Count > 1)
            {
                GenerateRoadAndSidewalk(points);
            }
        }
    }

    void GenerateDetailedBuilding(List<Vector3> polygon, int levels, Color color)
    {
        GameObject bObj = new GameObject("Building");
        MeshFilter mf = bObj.AddComponent<MeshFilter>();
        MeshRenderer mr = bObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = buildingMaterial;

        // Configura cor via Property Block para otimizar draw calls
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        propBlock.SetColor("_Color", color);
        mr.SetPropertyBlock(propBlock);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float floorHeight = 3.5f;
        float totalHeight = levels * floorHeight;

        // Geração de Paredes Segmentadas por Janelas
        for (int i = 0; i < polygon.Count - 1; i++)
        {
            Vector3 start = polygon[i];
            Vector3 end = polygon[i + 1];
            
            float segmentLength = Vector3.Distance(start, end);
            int windowColumns = Mathf.Max(1, Mathf.RoundToInt(segmentLength / 3.0f)); 

            for (int lvl = 0; lvl < levels; lvl++)
            {
                float bottomY = lvl * floorHeight;
                float topY = bottomY + floorHeight;

                for (int col = 0; col < windowColumns; col++)
                {
                    float tStart = (float)col / windowColumns;
                    float tEnd = (float)(col + 1) / windowColumns;

                    Vector3 v0 = Vector3.Lerp(start, end, tStart) + Vector3.up * bottomY;
                    Vector3 v1 = Vector3.Lerp(start, end, tStart) + Vector3.up * topY;
                    Vector3 v2 = Vector3.Lerp(start, end, tEnd) + Vector3.up * topY;
                    Vector3 v3 = Vector3.Lerp(start, end, tEnd) + Vector3.up * bottomY;

                    int baseIdx = vertices.Count;
                    vertices.AddRange(new Vector3[] { v0, v1, v2, v3 });

                    // UVs mapeando o quad diretamente para a região da janela no atlas
                    uvs.AddRange(new Vector2[] {
                        new Vector2(0, 0), new Vector2(0, 1), 
                        new Vector2(1, 1), new Vector2(1, 0)
                    });

                    triangles.AddRange(new int[] {
                        baseIdx, baseIdx + 1, baseIdx + 2,
                        baseIdx, baseIdx + 2, baseIdx + 3
                    });
                }
            }
        }

        // TODO: Adicionar lógica de Ear Clipping para fechar o teto na altura totalHeight

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mf.mesh = mesh;
    }

    void GenerateRoadAndSidewalk(List<Vector3> path)
    {
        // TODO: Implementar extrusão perpendicular ao longo do path
        // Criar malha central para a estrada (Asfalto com UV longitudinal)
        // Criar malha paralela com elevação Y=0.15f para a calçada (Petit-Pavé)
    }
}
```

---

## 4. Otimização e Performance Urbanística
1.  **Static Batching & Mesh Combining:** Agrupar calçadas e estradas em super-malhas únicas estáticas por quadrantes para minimizar sobrecarga de CPU na renderização.
2.  **Shader / Texture Atlas:** Utilizar apenas 1 Material com um Shader do tipo *Trim Sheet* ou *Texture Atlas* contendo a variação das janelas e texturas, permitindo o uso eficiente de GPU Instancing.