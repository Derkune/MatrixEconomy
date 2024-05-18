using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


public class SimDisplayRoot : Node2D
{
    public const int ChunksSide = 32;
    public const int NumberOfSystems = 128;
    public static Texture DotTex = GD.Load("res://starTexture.png") as Texture;

    private SparseMatrix _SectorAdjacencyMatrix;
    private SparseMatrix _MetricAdjacencyMatrix;
    private Matrix _StateMatrix = Matrix.Random(Enum.GetValues(typeof(Mts)).Length, NumberOfSystems, 0, 1);
    //private Matrix _StateMatrix = new Matrix(Enum.GetValues(typeof(Mts)).Length, NumberOfSystems);
    //private Matrix _AdditionalModification;
    private Dictionary<(Vector2, Vector2), float> _Connections = new Dictionary<(Vector2, Vector2), float>(); 
    private Dictionary<Vector2, int> _SystemIndexes = new Dictionary<Vector2, int>();
    private Dictionary<Vector2, Sprite> _SystemsAndSprites = new Dictionary<Vector2, Sprite>();
    private Dictionary<(Vector2, Vector2), Line2D> _Lines = new Dictionary<(Vector2, Vector2), Line2D>();
    //private Matrix _SpecialMetrics;

    private int _MetricSelected = 0;
    private List<Color> _MetricColors;
    private bool _Paused = false;
    private int _DaysPassed = 0;


    public override void _Ready()
    {
        GD.Randomize();
        //GD.Seed(2);

        _GenerateSystems();

        _DrawConnections();

        _SectorAdjacencyMatrix = _ConstructSectorAdjMat();
        _MetricAdjacencyMatrix = _ConstructMetricAdjMat();

        //_SpecialMetrics = Matrix.Random(Enum.GetValues(typeof(SpMts)).Length, NumberOfSystems, 0, 1);
        //_AdditionalModification = _ConstructAdditionalModification();
        
        _SectorAdjacencyMatrix.Normalize();
        _MetricAdjacencyMatrix.Normalize();

        var itemList = GetNode<ItemList>("CanvasLayer/ItemList");
        foreach (Mts metric in Enum.GetValues(typeof(Mts)))
        {
            itemList.AddItem(metric.ToString());
        }
        _AssignColorsToMetrics();
    }


    private void _GenerateSystems()
    {
        Vector2 chunkSize = OS.WindowSize / ChunksSide;

        Func<Vector2, Sprite> generateSprite = (intPos) => {
            Sprite sp = new Sprite();
            sp.Texture = DotTex;
            sp.Scale = new Vector2(2,2);

            RandomNumberGenerator rng = new RandomNumberGenerator();
            rng.Seed = (ulong) intPos.GetHashCode();

            Vector2 plusOne = intPos + new Vector2(1,1);
            const float squeeze = 0.2f;
            Vector2 pos = new Vector2(rng.RandfRange(intPos.x + squeeze, plusOne.x - squeeze), rng.RandfRange(intPos.y + squeeze, plusOne.y - squeeze)) * chunkSize;
            sp.Position = pos;
            AddChild(sp);

            return sp;
        };

        Func<Vector2, int, bool> registerSystem = (pos, idx) => {
            _SystemsAndSprites.Add(pos, generateSprite(pos));
            _SystemIndexes.Add(pos, idx);
            //_SystemsByIndexes[idx] = pos;
            return true;
        };

        Vector2 startingPos = new Vector2(ChunksSide / 2, ChunksSide / 2);
        registerSystem(startingPos, 0);

        while (_SystemsAndSprites.Count < NumberOfSystems)
        {
            var systemArr = _SystemsAndSprites.Keys.ToArray();
            var randomSystem = systemArr[GD.Randi() % systemArr.Length];
            int offsetChoice = (int) GD.Randi() % 4;
            (int, int) offset;
            switch (offsetChoice)
            {
                case 0:
                    offset = (1, 0);
                    break;
                case 1:
                    offset = (0, 1);
                    break;
                case 2:
                    offset = (-1, 0);
                    break;
                default:
                    offset = (0, -1);
                    break;
            }
            Vector2 destSys = new Vector2(offset.Item1 + (int) randomSystem.x, offset.Item2 + (int) randomSystem.y);
            
            bool generateNewSystem = !_SystemsAndSprites.ContainsKey(destSys) &&
                                destSys.x >= 0 &&
                                destSys.y >= 0 &&
                                destSys.x < ChunksSide &&
                                destSys.y < ChunksSide;
            
            if (generateNewSystem)
            {
                registerSystem(destSys, _SystemsAndSprites.Count - 1);
            }

            bool toGenerateConn =  (_SystemsAndSprites.ContainsKey(destSys) &&
                                    GD.RandRange(0, 1) > 0.9 &&
                                    !_Connections.ContainsKey((randomSystem, destSys)) &&
                                    !_Connections.ContainsKey((destSys, randomSystem))) ||
                                generateNewSystem;
            
            if (toGenerateConn)
            {
                Vector2 dist = _SystemsAndSprites[randomSystem].Position - _SystemsAndSprites[destSys].Position;
                float distValue = (1 - (dist / chunkSize).Length() / 2) * 0.8f + 0.3f;
                _Connections.Add((randomSystem, destSys), distValue);
            }
        }
    }


    private void _DrawConnections()
    {
        foreach (var connection in _Connections.Keys)
        {
            Sprite sp1 = _SystemsAndSprites[connection.Item1];
            Sprite sp2 = _SystemsAndSprites[connection.Item2];
            _Lines.Add(connection, _CreateLine(sp1, sp2, _Connections[connection]));
        }
    }


    private Line2D _CreateLine(Sprite sp1, Sprite sp2, float brightness)
    {
        Vector2[] points = new Vector2[2];
        points[0] = sp1.Position;
        points[1] = sp2.Position;

        Line2D line = new Line2D();
        line.Points = points;
        line.DefaultColor = Colors.White * brightness;
        //line.Antialiased = true;
        line.Width = 3;
        AddChild(line);
        return line;
    }


    private void _AssignColorsToMetrics()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Seed = 0;

        _MetricColors = new List<Color>();
        foreach (Mts metric in Enum.GetValues(typeof(Mts)))
        {
            Color c = new Color(rng.Randf(), rng.Randf(), rng.Randf());
            _MetricColors.Add(c.Lightened(0.5f));
        }
    }


    public override void _PhysicsProcess(float delta)
    {
        base._PhysicsProcess(delta);

        var toUseDelta = delta * 2;
        if (!_Paused)
        {
            _Simulate(toUseDelta);
            GetNode<Label>("CanvasLayer/DaysPassedLabel").Text = _DaysPassed.ToString() + " days";
            _DaysPassed += 1;
        }
        
        foreach (var system in _SystemsAndSprites)
        {
            float val = _StateMatrix.Data[_MetricSelected, _SystemIndexes[system.Key]];
            system.Value.Modulate = _MetricColors[_MetricSelected] * val;
        }
    }


    private void _Simulate(float toUseDelta)
    {
        var averaged = _StateMatrix.RightMultiply(_SectorAdjacencyMatrix);;
        averaged = averaged.LeftMultiply(_MetricAdjacencyMatrix);

        var diff = averaged.Add(_StateMatrix, -1);
        var deltaM = diff.ElementwiseMultiply(toUseDelta);
        //deltaM = deltaM.ElementwiseMultiply(_AdditionalModification);
        _StateMatrix = _StateMatrix.Add(deltaM, 1);

        Matrix decayFactor = _StateMatrix.ElementwiseMultiply(-toUseDelta/1000);
        _StateMatrix = _StateMatrix.Add(decayFactor, 1);

        if (Engine.GetFramesDrawn() % 2 == 0)
        {
            _StateMatrix.PerformEvent((int) GD.Randi() % 5, 1, _SectorAdjacencyMatrix);
        }
        if (Engine.GetFramesDrawn() % 3 == 0)
        {
            _StateMatrix.PerformEvent((int) GD.Randi() % 4, -1, _SectorAdjacencyMatrix);
        }
    }


    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventMouseButton e && e.Pressed && e.ButtonIndex == (int) ButtonList.Left)
        {
            Vector2 intPos = (e.GlobalPosition * ChunksSide / OS.WindowSize).Snapped(new Vector2(1,1));
            if (_SystemIndexes.ContainsKey(intPos))
            {
                int sysIdx = _SystemIndexes[intPos];
                _StateMatrix.Data[_MetricSelected, sysIdx] = 1;
            }
        }

        if (@event is InputEventKey k && k.Pressed && k.Scancode == (int) KeyList.Space)
        {
            _Paused = !_Paused;
            var pausedStr = _Paused? "Paused" : "Unpaused";
            GetNode<Button>("CanvasLayer/PauseButton").Text = pausedStr + " (Space)";
        }
    }


    public SparseMatrix _ConstructSectorAdjMat()
    {
        Matrix dense = new Matrix(NumberOfSystems, NumberOfSystems);
        foreach (var connection in _Connections)
        {
            int idx1 = _SystemIndexes[connection.Key.Item1];
            int idx2 = _SystemIndexes[connection.Key.Item2];

            dense.Data[idx1, idx2] = connection.Value;
            dense.Data[idx2, idx1] = connection.Value;
        }
        for (int i = 0; i < NumberOfSystems; i++)
        {
            dense.Data[i, i] = 1;
        }
        return new SparseMatrix(dense, false);
    }


    public SparseMatrix _ConstructMetricAdjMat()
    {
        int metricNum = Enum.GetValues(typeof(Mts)).Length;
        Matrix dense = new Matrix(metricNum, metricNum);
        foreach (var relationsipArr in MetricRelationships)
        {
            int metric1Idx = (int) relationsipArr.Key;
            foreach (var relationship in relationsipArr.Value)
            {
                int metric2Idx = (int) relationship.Item1;
                dense.Data[metric2Idx, metric1Idx] = (float) relationship.Item2;
            }
        }
        for (int i = 0; i < metricNum; i++)
        {
            dense.Data[i, i] = 1;
        }
        return new SparseMatrix(dense, true);
    }

    /*
    public Matrix _ConstructAdditionalModification()
    {
        float[] sumsOfMetrics = _SpecialMetrics.GetSumsOfLines(false);
        int amount = Enum.GetValues(typeof(SpMts)).Length;
        float[] averagesOfMetrics = sumsOfMetrics.Select((sum) => sum / amount).ToArray();
        var additionalModification = Matrix.FilledLines(Enum.GetValues(typeof(Mts)).Length, NumberOfSystems, false, averagesOfMetrics);

        return additionalModification;
    }
    */

    public void _on_ChooseMetricMenuButton_pressed()
    {
        var lst = GetNode<ItemList>("CanvasLayer/ItemList");
        lst.Visible = !lst.Visible;
    }


    public void _on_ItemList_item_selected(int item)
    {
        _MetricSelected = item;
    }


    public void _on_LinesVisibilityButton_pressed()
    {
        bool visible = _Lines.Values.ToArray()[0].Visible;
        foreach (Line2D line in _Lines.Values)
        {
            line.Visible = !visible;
        }
    }


    public void _on_PauseButton_pressed()
    {
        _Paused = !_Paused;
        var pausedStr = _Paused? "Paused" : "Unpaused";
        GetNode<Button>("CanvasLayer/PauseButton").Text = pausedStr + " (Space)";
    }


    public static Dictionary<Mts, (Mts, double)[]> MetricRelationships = new Dictionary<Mts, (Mts, double)[]> {
        {Mts.TradeActivity, new (Mts, double)[] {
            (Mts.Ore, 1),
            (Mts.Aliens, 1),
            (Mts.Crime, 1),
            (Mts.MedicalWares, 1),
            (Mts.Happiness, 1),
            (Mts.Luxuries, 1),
        }},
        {Mts.Ore, new (Mts, double)[] {
            (Mts.LowTechWares, 1),
            (Mts.RefinedOre, 1),
            (Mts.Ammo, 1),
        }},
        {Mts.RefinedOre, new (Mts, double)[] {
            (Mts.Alloys, 1),
            (Mts.HighTechWares, 1),
        }},
        {Mts.Alloys, new (Mts, double)[] {
            (Mts.LowTechWares, 1),
            (Mts.Ore, 1),
            (Mts.ShipParts, 1),
            (Mts.HighTechWares, 1),
        }},
        {Mts.LowTechWares, new (Mts, double)[] {
            (Mts.HighTechWares, 1),
        }},
        {Mts.HighTechWares, new (Mts, double)[] {
            (Mts.ShipParts, 1),
            (Mts.Aliens, 1),
            (Mts.MedicalWares, 1),
            (Mts.Ore, 1),
        }},
        {Mts.Aliens, new (Mts, double)[] {
            (Mts.AlienArtifacts, 1),
            (Mts.Debris, 1),
        }},
        {Mts.MedicalWares, new (Mts, double)[] {
            (Mts.Narcotics, 1),
            (Mts.Happiness, 1),
        }},
        {Mts.AlienArtifacts, new (Mts, double)[] {
            (Mts.HighTechWares, 1),
            (Mts.ShipParts, 1),
            (Mts.Aliens, 1),
        }},
        {Mts.ShipParts, new (Mts, double)[] {
            (Mts.Crime, 1),
        }},
        {Mts.Crime, new (Mts, double)[] {
            (Mts.Debris, 1),
            (Mts.Narcotics, 1),
        }},
        {Mts.Debris, new (Mts, double)[] {
            (Mts.LowTechWares, 1),
            (Mts.Alloys, 1),
            (Mts.ShipParts, 1),
            (Mts.Ammo, 1),
        }},
        {Mts.Narcotics, new (Mts, double)[] {
            (Mts.Crime, 1),
            (Mts.Happiness, 1),
        }},
        {Mts.Ammo, new (Mts, double)[] {
            (Mts.Crime, 1),
        }},
        {Mts.Happiness, new (Mts, double)[] {
            (Mts.TradeActivity, 1),
            (Mts.Luxuries, 1),
        }},
        {Mts.Luxuries, new (Mts, double)[] {
            (Mts.Happiness, 1),
        }}
    };
}


public struct Matrix
{
    public readonly int H;
    public readonly int W;
    public float[,] Data;


    public Matrix(int he, int wi)
    {
        H = he;
        W = wi;
        Data = new float[H, W];
    }


    public Matrix(SparseMatrix sMatrix)
    {
        H = sMatrix.H;
        W = sMatrix.W;
        Data = new float[H, W];

        for (int i = 0; i < sMatrix.Data.Length; i++)
        {
            foreach (var tuple in sMatrix.Data[i])
            {
                int j = tuple.Item2;
                if (sMatrix.SequenceOfRows)
                {
                    Data[i, j] = tuple.Item1;
                }
                else
                {
                    Data[j, i] = tuple.Item1;
                }
            }
        }
    }


    public static Matrix Filled(int he, int wi, float value)
    {
        Matrix ma = new Matrix(he, wi);
        for (int co = 0; co < wi; co++)
        {
            for (int ro = 0; ro < he; ro++)
            {
                ma.Data[ro, co] = value;
            }
        }
        return ma;
    }


    public static Matrix Random(int he, int wi, float lowerBound, float upperBound)
    {
        Matrix ma = new Matrix(he, wi);
        for (int co = 0; co < wi; co++)
        {
            for (int ro = 0; ro < he; ro++)
            {
                ma.Data[ro, co] = (float) GD.RandRange(lowerBound, upperBound);
            }
        }
        return ma;
    }


    public static Matrix FilledLines(int he, int wi, bool linesAreRows, float[] valuesOfLines)
    {
        Matrix ma = new Matrix(he, wi);

        if (linesAreRows)
        {
            for (int ro = 0; ro < he; ro++)
            {
                float val = valuesOfLines[ro];
                for (int co = 0; co < wi; co++)
                {
                    ma.Data[ro, co] = val;
                }
            }
        }
        else
        {
            for (int co = 0; co < wi; co++)
            {
                float val = valuesOfLines[co];
                for (int ro = 0; ro < he; ro++)
                {
                    ma.Data[ro, co] = val;
                }
            }
        }
        return ma;

    }


    public float[] GetSumsOfLines(bool linesAreRows)
    {
        int size = linesAreRows? H : W;
        int size2 = linesAreRows? W : H;
        float[] sums = new float[size];
        for (int i = 0; i < size; i++)
        {
            float sum = 0;

            for (int j = 0; j < size2; j++)
            {
                float val = linesAreRows ? Data[i, j] : Data[j, i];
                sum += val;
            }
            sums[i] = sum;
        }
        return sums;
    }


    public Matrix RightMultiply(SparseMatrix arg)
    {
        if (arg.SequenceOfRows)
        {
            throw new Exception("Not the right type of sequence!");
        }
        if (this.W != arg.H)
        {
            throw new Exception("Not the right shape!");
        }

        var output = new Matrix(H, arg.W);

        for (int ro = 0; ro < output.H; ro++)
        {
            for (int co = 0; co < output.W; co++)
            {
                float val = 0;
                foreach (var tuple in arg.Data[co])
                {
                    val += tuple.Item1 * Data[ro, tuple.Item2];
                }
                output.Data[ro, co] = val;
            }
        }
        return output;
    }


    public Matrix LeftMultiply(SparseMatrix arg)
    {
        if (!arg.SequenceOfRows)
        {
            throw new Exception("Not the right type of sequence!");
        }
        if (arg.W != this.H)
        {
            throw new Exception("Not the right shape!");
        }

        var output = new Matrix(arg.H, W);

        for (int ro = 0; ro < output.H; ro++)
        {
            for (int co = 0; co < output.W; co++)
            {
                float val = 0;
                foreach (var tuple in arg.Data[ro])
                {
                    val += tuple.Item1 * Data[tuple.Item2, co];
                }
                output.Data[ro, co] = val;
            }
        }
        return output;
    }


    public Matrix ElementwiseMultiply(float mul)
    {
        Matrix retVal = new Matrix(H, W);
        for (int co = 0; co < W; co++)
        {
            for (int ro = 0; ro < H; ro++)
            {
                retVal.Data[ro, co] = Data[ro, co] * mul;
            }
        }
        return retVal;
    }


    public Matrix ElementwiseMultiply(Matrix ma)
    {
        if (ma.H != H || ma.W != W)
        {
            throw new Exception("Not the right shape!");
        }

        Matrix retVal = new Matrix(H, W);
        for (int co = 0; co < W; co++)
        {
            for (int ro = 0; ro < H; ro++)
            {
                retVal.Data[ro, co] = Data[ro, co] * ma.Data[ro, co];
            }
        }
        return retVal;
    }

    //subtract by setting mul to -1
    public Matrix Add(Matrix ma, float mul)
    {
        if (ma.H != H || ma.W != W)
        {
            throw new Exception("Not the right shape!");
        }

        Matrix retVal = new Matrix(H, W);
        for (int co = 0; co < W; co++)
        {
            for (int ro = 0; ro < H; ro++)
            {
                retVal.Data[ro, co] = Data[ro, co] + mul * ma.Data[ro, co];
            }
        }
        return retVal;
    }


    public void SetRandomCells(float value, int numOfCells)
    {
        for (int i = 0; i < numOfCells; i++)
        {
            int x = (int) (GD.Randi() % W);
            int y = (int) (GD.Randi() % H);
            Data[y, x] = value;
        }
    }

    //sets values to val in radius around a random sector
    public void PerformEvent(int radius, float val, SparseMatrix sectorMatrix)
    {
        if (sectorMatrix.SequenceOfRows)
        {
            throw new Exception("Not the right format of sector matrix!");
        }
        int metricChosen = (int) (GD.Randi() % H);
        List<int> currentIteration = new List<int>();
        currentIteration.Add((int) (GD.Randi() % W));        

        for (int rad = 0; rad < radius; rad++)
        {
            List<int> nextIteration = new List<int>();
            foreach (int iteratedSector in currentIteration)
            {
                Data[metricChosen, iteratedSector] = val;

                foreach (var neighrborTuple in sectorMatrix.Data[iteratedSector])
                {
                    nextIteration.Add(neighrborTuple.Item2);
                }
            }
            currentIteration = nextIteration;
        }
    }


    public bool IsSymmetric()
    {
        for (int co = 0; co < W; co++)
        {
            for (int ro = 0; ro < H; ro++)
            {
                if (Data[co, ro] != Data[ro, co])
                {
                    return false;
                }
            }
        }

        return true;
    }


    public string ToString(bool full = false)
    {
        Action<float, StringBuilder> appendValue = (value, toBuilder) => {
            toBuilder.Append(" ");
            toBuilder.Append(value.ToString("0.00"));
            toBuilder.Append(" ");
        };
        Action<int, StringBuilder, Matrix> appendRow = (ro, buildr, @this) => {
            buildr.Append(" [");
            int maxValR = full ? @this.W : 3;
            for (int co = 0; co < Mathf.Min(@this.W, maxValR); co++)
            {
                appendValue(@this.Data[ro, co], buildr);
            }
            if (!full && @this.W >= 3)
            {
                buildr.Append("...");
                appendValue(@this.Data[ro, @this.W - 1], buildr);
            }
            buildr.Append("]\n");
        };

        StringBuilder builder = new StringBuilder();
        builder.Append("[H: " + H.ToString() + ", W:" + W.ToString() + "\n");
        int maxVal = full ? H : 3;
        for (int ro = 0; ro < Mathf.Min(H, maxVal); ro++)
        {
            appendRow(ro, builder, this);
        }
        if (!full && H >= 3)
        {
            builder.Append("  ...\n");
            appendRow(H - 1, builder, this);
        }
        builder.Append("]");
        return builder.ToString();
    }
}


public struct SparseMatrix
{
    public readonly bool SequenceOfRows;
    public readonly (float, int)[][] Data;
    public readonly int H;
    public readonly int W;


    public SparseMatrix(Matrix inputM, bool sequenceOfRows)
    {
        SequenceOfRows = sequenceOfRows;
        H = inputM.H;
        W = inputM.W;

        int numOfSequences = sequenceOfRows ? inputM.H : inputM.W;
        List<(float, int)[]> sequence = new List<(float, int)[]>();
        for (int i = 0; i < numOfSequences; i++)
        {
            int lineLength = sequenceOfRows ? inputM.W : inputM.H;
            List<(float, int)> compressedLine = new List<(float, int)>();
            for (int j = 0; j < lineLength; j++)
            {
                float value = sequenceOfRows ? inputM.Data[i, j] : inputM.Data[j, i];
                if (value != 0)
                {
                    compressedLine.Add((value, j));
                }
            }
            sequence.Add(compressedLine.ToArray());
        }
        Data = sequence.ToArray();
    }


    public string ToString(bool full = false)
    {
        return new Matrix(this).ToString(full);
    }


    public float GetAverageArrayLength()
    {
        float sum = 0;
        foreach (var line in Data)
        {
            sum += line.Length;
        }
        return sum / Data.Length;
    }

    //normalize each line
    public void Normalize()
    {
        foreach (var line in Data)
        {
            float sum = 0;
            foreach (var data in line)
            {
                sum += data.Item1;
            }
            for (int i = 0; i < line.Length; i++)
            {
                line[i] = (line[i].Item1 / sum, line[i].Item2);
            }
        }
    }
}


public enum Mts
{
    TradeActivity,
    Ore,
    RefinedOre,
    Alloys,
    LowTechWares,
    HighTechWares,
    Aliens,
    MedicalWares,
    AlienArtifacts,
    ShipParts,
    Crime,
    Debris,
    Narcotics,
    Ammo,
    Happiness,
    Luxuries,
}
/*
public enum SpMts
{
    Population,
    TechLevel,
    NaturalResources,
    Empire,
    Alliance,
    Federation,
    AlienConcentration,
    AmbientDebris,
}
*/
