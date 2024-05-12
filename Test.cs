using Godot;
using System;
using System.Collections.Generic;
using NumSharp;

public class Test : Node
{
    public override void _Ready()
    {
        ulong beg = OS.GetTicksMsec();
        var a = new Matrix(1000, 1000);
        var a2 = new Matrix(50, 50);
        GD.Print(OS.GetTicksMsec() - beg);

        for (int i = 0; i < 1000; i++)
        {
            a.Data[i, i] = i / 1000f;
        }
        for (int i = 0; i < 50; i++)
        {
            a2.Data[i,i] = i / 50f;
        }
        var aS = new SparseMatrix(a, false);
        var a2S = new SparseMatrix(a2, true);
        GD.Print(aS.ToString());
        GD.Print(a2S.ToString());
        GD.Print(OS.GetTicksMsec() - beg);

        var b = Matrix.Filled(50, 1000, 1);
        GD.Print(b.ToString());
        GD.Print(OS.GetTicksMsec() - beg);

        var c = b.RightMultiply(aS).LeftMultiply(a2S);
        GD.Print(c.ToString());
        GD.Print(OS.GetTicksMsec() - beg);


        var shapeA = NumSharp.Shape.Matrix(1000, 1000);
        var shapeA2 = NumSharp.Shape.Matrix(50, 50);
        var shapeB = NumSharp.Shape.Matrix(50, 1000);

        var tA = new NDArray(a.Data, shapeA);
        var tA2 = new NDArray(a2.Data, shapeA2);
        var tB = new NDArray(b.Data, shapeB);
        var tC = new NDArray(c.Data, shapeB);
        GD.Print(OS.GetTicksMsec() - beg);

        GD.Print(tA2.dot(tB.dot(tA)).array_equal(tC));
        GD.Print(OS.GetTicksMsec() - beg);

    }
}
