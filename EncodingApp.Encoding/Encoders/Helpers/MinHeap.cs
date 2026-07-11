using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncodingApp.Encoding.Encoders.Helpers;

class MinHeap<T> where T : IComparable<T>
{
    private readonly T[] _array;
    public int Count { get; private set; }

    public MinHeap(int capacity) { _array = new T[capacity]; }

    public void Push(T item)
    {
        int i = Count++;
        _array[i] = item;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_array[i].CompareTo(_array[parent]) >= 0) break;
            (_array[i], _array[parent]) = (_array[parent], _array[i]);
            i = parent;
        }
    }

    public T Pop()
    {
        T root = _array[0];
        T last = _array[--Count];
        if (Count > 0)
        {
            _array[0] = last;
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1;
                int right = left + 1;
                int smallest = i;
                if (left < Count && _array[left].CompareTo(_array[smallest]) < 0) smallest = left;
                if (right < Count && _array[right].CompareTo(_array[smallest]) < 0) smallest = right;
                if (smallest == i) break;
                (_array[i], _array[smallest]) = (_array[smallest], _array[i]);
                i = smallest;
            }
        }
        return root;
    }

    public T Peek() => _array[0];
}