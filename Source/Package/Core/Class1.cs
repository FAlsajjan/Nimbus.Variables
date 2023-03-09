
using System;
using System.Collections.Generic;
using System.Linq;
using Nimbus.Channels;
using Nimbus.Common;
using Nimbus.DI;
using Nimbus.Systems;
using Nimbus.Systems.Unity;
using Nimbus.Tasks;
using UnityEngine;

namespace Nimbus.Variables
{

    public class VariableSystem : NSystemBase , IUpdateableSystem
    {
        

        [Inject()] private DIContainer _container;
        private FixedTypeKeyHashtable<List<IVariable>> _variables;
        private List<IChannel> _channels;
        public override void OnStarted()
        {
            _variables = new FixedTypeKeyHashtable<List<IVariable>>();
            _channels = new List<IChannel>();
        }

        private List<object> _fillList = new List<object>();
        public void ListenTo(IChannel channel)
        {
            _channels.Add(channel);
        }

        public void AddVariable(IVariable variable)
        {
            _container.Inject(variable);
            foreach (var trigger in variable.GetUpdateTriggers())
            {
                if (!_variables.TryGet(trigger, out List<IVariable> vars))
                {
                    vars = new List<IVariable>();
                    _variables.Set(trigger,vars);
                }
                vars.Add(variable);
            }
            
        }
        public override void OnDestroy()
        {
            
        }

        public void OnUpdate()
        {
            foreach (var channel in _channels)
            {
                foreach (var o in channel.PollAll())
                {
                    if (_variables.TryGet(o.GetType(), out List<IVariable> vars))
                    {
                        foreach (var variable in vars)
                        {
                            variable.Evaluate();
                        }
                    }
                }
            }
            
        }

        public PlayerLoopTiming PreferredTiming { get => PlayerLoopTiming.EarlyUpdate; }
    }

    public interface IVariable
    {
        string Name { get; }
        object Evaluate();

        NEvent OnUpdated { get; }
        IEnumerable<Type> GetUpdateTriggers();
    }

    public interface IVariable<T> : IVariable
    {
        new T Evaluate();
        NEvent<T> OnChanged { get; }

    }

    
    public abstract class Variable<T> : IVariable<T>
    {
        public abstract string Name { get; protected set; }

        private T _lastValue;

        public T Evaluate()
        { 
            T value = OnEvaluate();
            if (ShouldUpdate(_lastValue,value))
            {
                _lastValue = value;
                OnChanged.Invoke(value);
            }
            OnUpdated.Invoke();

            return value;
        }

        protected virtual bool ShouldUpdate(T oldValue,T newValue)
        {
            return !(oldValue.Equals(newValue));
        }
        protected abstract T OnEvaluate();
        public NEvent<T> OnChanged { get; } = new NEvent<T>();
        public virtual IEnumerable<Type> GetUpdateTriggers()
        {
            
                yield break;
            
        }

        public NEvent OnUpdated { get; } = new NEvent();


        object IVariable.Evaluate()
        {
           
            return Evaluate();
        }
    }

    public class ConstantVariable<T> : Variable<T>
    {
        private readonly T _value;
        public override string Name { get; protected set; }

        public ConstantVariable(string name, T value)
        {
            this._value = value;
        }

        protected override T OnEvaluate()
        {
            return _value;
        }
    }
    public abstract class Condition<T> : Variable<bool>
    {
        private readonly IComparer<T> _comparer;
        public IVariable<T> LHS { get; private set; }
        public IVariable<T> RHS { get; private set; }
        public override string Name { get; protected set; }
        public Condition(IVariable<T> lhs, IVariable<T> rhs, IComparer<T> comparer)
        {
            this.LHS = lhs;
            this.RHS = rhs;
            this._comparer = comparer;
        }
        protected override bool OnEvaluate()
        {
            return IsComparisonApplicable(_comparer.Compare(LHS.Evaluate(), RHS.Evaluate()));
        }

        public override IEnumerable<Type> GetUpdateTriggers()
        {
            return LHS.GetUpdateTriggers().Concat(RHS.GetUpdateTriggers());
        }

        protected abstract bool IsComparisonApplicable(int comparison);
    }
    
}