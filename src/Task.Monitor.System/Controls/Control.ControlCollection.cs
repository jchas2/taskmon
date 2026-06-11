using System.Collections;

namespace Task.Monitor.System.Controls;

public class ControlCollection : IEnumerable<Control>
{
    private readonly Control owner;
    
    public ControlCollection(Control owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    
    public ControlCollection Add(Control control)
    {
        ArgumentNullException.ThrowIfNull(control, nameof(control));
        
        owner.InsertControls([control]);

        return this;
    }

    public void AddRange(params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(controls, nameof(controls));
        
        owner.InsertControls(controls);
    }

    public void Clear() => owner.ClearControls();

    public bool Contains(Control control)
    {
        ArgumentNullException.ThrowIfNull(control, nameof(control));
        
        return owner.ContainsControl(control);
    }

    public int Count => owner.ControlCount;

    public IEnumerator<Control> GetEnumerator()
    {
        /* Shallow copy the items and return an enumerator off that container. */
        List<Control> items = new(owner.Controls.Count);
        
        for (int i = 0; i < owner.ControlCount; i++) {
            items.Add(owner.GetControlByIndex(i));
        }
        
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(Control control)
    {
        ArgumentNullException.ThrowIfNull(control, nameof(control));
        
        return owner.IndexOfControl(control);
    }

    public void Remove(Control control)
    {
        ArgumentNullException.ThrowIfNull(control, nameof(control));
        
        owner.RemoveControl(control);
    }
    
    public Control this[int index]
    {
        get {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
            
            return owner.GetControlByIndex(index);
        }
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
            
            owner.RemoveControlAt(index);
            owner.InsertControl(index, value);
        }
    }
}