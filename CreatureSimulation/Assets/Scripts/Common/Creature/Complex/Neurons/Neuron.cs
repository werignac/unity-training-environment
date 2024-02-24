using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace werignac.Creatures
{
	using OUT_CONNECTION = Tuple<SignalReceiver<float>, int>;

	public enum NeuronType
	{
		SUM,
		PRODUCT,
		DIVIDE,
		SUM_THRESHOLD,
		GREATER_THAN,
		SIGN_OF,
		MIN,
		MAX,
		ABS,
		IF,
		INTERPOLATE,
		SIN,
		COS,
		ATAN,
		LOG,
		EXPONENT,
		SIGMOID,
		INTEGRATE,
		DIFFERENTIATE,
		SMOOTH,
		MEMORY,
		OSCIALLATE_WAVE,
		OSCIALLATE_SAW,
		// William's Additions
		DOT_PRODUCT_VEC3,
		DOT_PRODUCT_VEC2,
		INTERPOLATE_ANGLE,
		INTERPOLATE_UNCLAMPED,
		NEGATE,
		IS_NAN,
		DEBUG = -1
	}

	public interface SignalSender
	{
		public void SetOutConnection(SignalReceiver<float> _toSet, int index);
		public void AddOutConnections(params OUT_CONNECTION[] connections);
		public void Evaluate();
	}

	public interface SignalReceiver<T>
	{
		Signal<T> GetSignal();
	}

	public class Constant : SignalSender
	{
		private float value;
		private List<OUT_CONNECTION> outConnections = new List<OUT_CONNECTION>();

		public Constant(float _value)
		{
			value = _value;
		}

		public static Constant InstantiateRandom()
		{
			return new Constant(GetRandomConstant());
		}
		private static float GetRandomConstant()
		{
			float[] importantConstants = new float[] { -1, 0, Mathf.Epsilon, 1, 1.618f, 2, Time.deltaTime, 2.718f, Mathf.PI, 5, 10, 100 };

			if (UnityEngine.Random.value > 0.5f)
			{
				return importantConstants[UnityEngine.Random.Range(0, importantConstants.Length)];
			} else
			{
				return UnityEngine.Random.Range(float.MinValue, float.MaxValue);
			}
		}

		public void AddOutConnections(params OUT_CONNECTION[] connections)
		{
			outConnections.AddRange(connections);
		}

		public void Evaluate()
		{
			foreach (OUT_CONNECTION connection in outConnections)
				connection.Item1.GetSignal().SetInput(value, connection.Item2);
		}

		public void SetOutConnection(SignalReceiver<float> _toSet, int index)
		{
			outConnections.Add(new OUT_CONNECTION(_toSet, index));
		}

		public bool SameConstantValue(Constant other)
		{
			return value == other.value;
		}
	}

	public abstract class Neuron : SignalReceiver<float>, SignalSender
	{
		public NeuronType Type
		{
			get;
			private set;
		}

		public int InputCount
		{
			get { return nextSignal.Inputs.Length; }
		}

		private Signal<float> nextSignal;
		private Signal<float> lastSignal;
		private List<OUT_CONNECTION> outConnections = new List<OUT_CONNECTION>();

		public Neuron(NeuronType _type, int _inputCount)
		{
			Type = _type;
			nextSignal = new Signal<float>(_inputCount);
		}

		public void SetOutConnection(SignalReceiver<float> _toSet, int index)
		{
			outConnections.Add(new OUT_CONNECTION(_toSet, index));
		}

		public void AddOutConnections(params OUT_CONNECTION[] connections)
		{
			outConnections.AddRange(connections);
		}

		public void Evaluate()
		{
			if (lastSignal.State == Signal<float>.SignalState.CONSUMABLE)
			{
				float[] ins = lastSignal.Inputs;
				bool evaluated = EvaluationFunction(out float toSend, ins);

				if (!evaluated)
				{
					foreach (OUT_CONNECTION connection in outConnections)
						connection.Item1.GetSignal().Cancel();
					return;
				}

				foreach (OUT_CONNECTION connection in outConnections)
					connection.Item1.GetSignal().SetInput(toSend, connection.Item2);
			}
		}

		protected abstract bool EvaluationFunction(out float toSend, params float[] ins);

		public void Progress()
		{
			lastSignal = nextSignal;
			nextSignal = new Signal<float>(InputCount);
		}

		public Signal<float> GetSignal()
		{
			return nextSignal;
		}
	}

	public abstract class AlwaysEvaluateNeuron : Neuron
	{
		public AlwaysEvaluateNeuron(NeuronType _type, int _inputCount) : base(_type, _inputCount) { }

		protected override bool EvaluationFunction(out float toSend, params float[] ins)
		{
			toSend = AlwaysEvaluate(ins);
			return true;
		}

		protected abstract float AlwaysEvaluate(params float[] ins);
	}

	public abstract class SingleInNeuron : AlwaysEvaluateNeuron
	{
		public SingleInNeuron(NeuronType _type) : base(_type, 1) { }

		protected override float AlwaysEvaluate(params float[] ins)
		{
			return Operation(ins[0]);
		}

		protected abstract float Operation(float x);
	}

	public abstract class DoubleInNeuron : AlwaysEvaluateNeuron
	{
		public DoubleInNeuron(NeuronType _type) : base(_type, 2) { }

		protected override float AlwaysEvaluate(params float[] ins)
		{
			return Operation(ins[0], ins[1]);
		}

		protected abstract float Operation(float x, float y);
	}

	public abstract class TripleInNeuron : AlwaysEvaluateNeuron
	{
		public TripleInNeuron(NeuronType _type) : base(_type, 3) { }

		protected override float AlwaysEvaluate(params float[] ins)
		{
			return Operation(ins[0], ins[1], ins[2]);
		}

		protected abstract float Operation(float x, float y, float z);
	}

	// --- LEAF NEURONS ---

	public class SumNeuron : DoubleInNeuron
	{
		public SumNeuron() : base(NeuronType.SUM) {}
		protected override float Operation(float x, float y) { return x + y; }
	}

	public class ProductNeuron : DoubleInNeuron
	{
		public ProductNeuron() : base(NeuronType.PRODUCT) {}
		protected override float Operation(float x, float y) { return x * y; }
	}

	public class DivideNeuron : DoubleInNeuron
	{
		public DivideNeuron() : base(NeuronType.DIVIDE) {}
		protected override float Operation(float x, float y) { return x / y; }
	}

	public class SumThresholdNeuron : TripleInNeuron
	{
		public SumThresholdNeuron() : base(NeuronType.SUM_THRESHOLD) { }

		protected override float Operation(float x, float y, float threshold)
		{
			return Mathf.Min(x + y, threshold);
		}
	}

	public class GreaterThanNeuron : DoubleInNeuron
	{
		public GreaterThanNeuron() : base(NeuronType.GREATER_THAN) { }
		protected override float Operation(float x, float y) { return (x > y) ? 1f : 0f; }
	}

	public class SignOfNeuron : SingleInNeuron
	{
		public SignOfNeuron() : base(NeuronType.SIGN_OF) {}
		protected override float Operation(float x)
		{
			if (x > 0)
				return 1f;
			else if (x < 0)
				return -1f;
			else
				return 0f;
		}
	}

	public class MinNeuron : DoubleInNeuron
	{
		public MinNeuron() : base(NeuronType.MIN) {}
		protected override float Operation(float x, float y)
		{
			return Mathf.Min(x, y);
		}
	}

	public class MaxNeuron : DoubleInNeuron
	{
		public MaxNeuron() : base(NeuronType.MAX) { }
		protected override float Operation(float x, float y)
		{
			return Mathf.Max(x, y);
		}
	}

	public class AbsNeuron : SingleInNeuron
	{
		public AbsNeuron() : base(NeuronType.ABS) { }
		protected override float Operation(float x)
		{
			return Mathf.Abs(x);
		}
	}

	public class IfNeuron : Neuron
	{
		public IfNeuron() : base(NeuronType.IF, 2) { }

		/// <param name="toSend"></param>
		/// <param name="ins">ins[0] = condition (0 = false else true); ins[1] = value to pass on</param>
		/// <returns></returns>
		protected override bool EvaluationFunction(out float toSend, params float[] ins)
		{
			toSend = ins[1];
			return ins[0] != 0;
		}
	}

	public class InterpolateNeuron : TripleInNeuron
	{
		public InterpolateNeuron() : base(NeuronType.INTERPOLATE) { }
		protected override float Operation(float x, float y, float z)
		{
			return Mathf.Lerp(x, y, z);
		}
	}

	public class SinNeuron : SingleInNeuron
	{
		public SinNeuron() : base(NeuronType.SIN) { }
		protected override float Operation(float x)
		{
			return Mathf.Sin(x);
		}
	}

	public class CosNeuron : SingleInNeuron
	{
		public CosNeuron() : base(NeuronType.COS) { }
		protected override float Operation(float x)
		{
			return Mathf.Cos(x);
		}
	}

	public class ATanNeuron : SingleInNeuron
	{
		public ATanNeuron() : base(NeuronType.ATAN) { }
		protected override float Operation(float x)
		{
			return Mathf.Atan(x);
		}
	}

	public class LogNeuron : DoubleInNeuron
	{
		public LogNeuron() : base(NeuronType.LOG) { }
		protected override float Operation(float x, float y)
		{
			return Mathf.Log(x, y);
		}
	}

	public class ExponentNeuron : DoubleInNeuron
	{
		public ExponentNeuron() : base(NeuronType.EXPONENT) { }
		protected override float Operation(float x, float y)
		{
			return Mathf.Pow(x, y);
		}
	}

	public class SigmoidNeuron : SingleInNeuron
	{
		public SigmoidNeuron() : base(NeuronType.SIGMOID) {}
		protected override float Operation(float x)
		{
			// From Wikipedia: https://en.wikipedia.org/wiki/Sigmoid_function
			return 1 / (1 + Mathf.Exp(x));
		}
	}

	public class IntegrateNeuron : SingleInNeuron
	{
		private float runningIntegration = 0;
		public IntegrateNeuron() : base(NeuronType.INTEGRATE) { }
		protected override float Operation(float x)
		{
			runningIntegration += x * Time.fixedDeltaTime;
			return runningIntegration;
		}
	}

	public class DerivativeNeuron : SingleInNeuron
	{
		private float lastValue = 0;
		public DerivativeNeuron() : base(NeuronType.DIFFERENTIATE) { }
		protected override float Operation(float x)
		{
			float rise = x - lastValue;
			float run = Time.fixedDeltaTime;

			lastValue = x;

			return rise / run;
		}
	}

	/// <summary>
	/// Implements exponential smoothing with a
	/// constant smoothing value.
	/// 
	/// https://en.wikipedia.org/wiki/Exponential_smoothing
	/// </summary>
	public class SmoothNeuron : SingleInNeuron
	{
		private const float smoothingFactor = 0.5f;
		private float smoothingProgress = 0f;
		private bool hasGottenAValue = false;
		public SmoothNeuron() : base(NeuronType.SMOOTH) { }
		protected override float Operation(float x)
		{
			if (hasGottenAValue)
			{
				smoothingProgress = smoothingFactor * x + (1 - smoothingFactor) * smoothingProgress;
			} else {
				hasGottenAValue = true;
				smoothingProgress = x;
			}

			return smoothingProgress;
		}
	}

	public class MemoryNeuron : SingleInNeuron
	{
		private float last;
		public MemoryNeuron() : base(NeuronType.MEMORY) { }
		protected override float Operation(float x)
		{
			float toReturn = last;
			last = x;
			return toReturn;
		}
	}

	/// <summary>
	/// Creates a value that oscillates between zero and height over time.
	/// </summary>
	public class OsciallatingWaveNeuron : SingleInNeuron
	{
		private const float period = 1f;
		private float time;
		public OsciallatingWaveNeuron() : base(NeuronType.OSCIALLATE_WAVE) { }
		protected override float Operation(float height)
		{
			time = (time + Time.deltaTime) % period;
			return Mathf.Cos(time * 2 * Mathf.PI / period + Mathf.PI ) * height / 2 + height / 2;
		}
	}

	/// <summary>
	/// Creates a value that osciallates between zero and height in a saw pattern.
	/// https://en.wikipedia.org/wiki/Sawtooth_wave
	/// </summary>
	public class OsciallatingSawNeuron : SingleInNeuron
	{
		private const float period = 1f;
		private float time;
		public OsciallatingSawNeuron() : base(NeuronType.OSCIALLATE_SAW) { }
		protected override float Operation(float height)
		{
			time = (time + Time.deltaTime) % period;
			return Mathf.Lerp(0, height, time / period);
		}
	}

	public class DotProductVec3Neuron : AlwaysEvaluateNeuron
	{
		public DotProductVec3Neuron() : base(NeuronType.DOT_PRODUCT_VEC3, 6) { }
		protected override float AlwaysEvaluate(params float[] ins)
		{
			Vector3 x = new Vector3(ins[0], ins[1], ins[2]);
			Vector3 y = new Vector3(ins[3], ins[4], ins[5]);

			return Vector3.Dot(x, y);
		}
	}

	public class DotProductVec2Neuron : AlwaysEvaluateNeuron
	{
		public DotProductVec2Neuron() : base(NeuronType.DOT_PRODUCT_VEC2, 4) { }
		protected override float AlwaysEvaluate(params float[] ins)
		{
			Vector2 x = new Vector2(ins[0], ins[1]);
			Vector2 y = new Vector2(ins[2], ins[3]);

			return Vector2.Dot(x, y);
		}
	}

	public class InterpolateAngleNeuron : TripleInNeuron
	{
		public InterpolateAngleNeuron() : base(NeuronType.INTERPOLATE_ANGLE) { }
		protected override float Operation(float x, float y, float z)
		{
			return Mathf.LerpAngle(x * Mathf.Rad2Deg, y * Mathf.Rad2Deg, z) * Mathf.Deg2Rad;
		}
	}

	public class InterpolateUnclampedNeuron : TripleInNeuron
	{
		public InterpolateUnclampedNeuron() : base(NeuronType.INTERPOLATE_UNCLAMPED) { }
		protected override float Operation(float x, float y, float z)
		{
			return Mathf.LerpUnclamped(x, y, z);
		}
	}

	public class NegateNeuron : SingleInNeuron
	{
		public NegateNeuron() : base(NeuronType.NEGATE) { }
		protected override float Operation(float x)
		{
			return -x;
		}
	}

	public class IsNanNeuron : SingleInNeuron
	{
		public IsNanNeuron() : base(NeuronType.IS_NAN) { }
		protected override float Operation(float x)
		{
			return (float.IsNaN(x)) ? 1 : 0;
		}
	}

	/// <summary>
	/// Neuron for debugging.
	/// </summary>
	/// 
	public class DebugNeuron : Neuron
	{
		private string formatMessage;
		public DebugNeuron(string _formatMessage, int inputCount) : base(NeuronType.DEBUG, inputCount) { formatMessage = _formatMessage; }
		protected override bool EvaluationFunction(out float toSend, params float[] ins)
		{
			toSend = ins[0];

			object[] ins_obj = new object[ins.Length];
			ins.CopyTo(ins_obj, 0);

			Debug.LogFormat(formatMessage, ins_obj);

			return true;
		}
	}

	// Neuron Type to Instance Mapping

	public static class NeuronFactory
	{
		static public Neuron InstantiateRandom()
		{
			// -1 for Debug Neuron
			return Instantiate((NeuronType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(NeuronType)).Length - 1));
		}
		
		static public Neuron Instantiate(NeuronType type)
		{
			switch(type)
			{
				case NeuronType.ABS:
					return new AbsNeuron();
				case NeuronType.ATAN:
					return new ATanNeuron();
				case NeuronType.COS:
					return new CosNeuron();
				case NeuronType.DIFFERENTIATE:
					return new DerivativeNeuron();
				case NeuronType.DIVIDE:
					return new DivideNeuron();
				case NeuronType.DOT_PRODUCT_VEC2:
					return new DotProductVec2Neuron();
				case NeuronType.DOT_PRODUCT_VEC3:
					return new DotProductVec3Neuron();
				case NeuronType.EXPONENT:
					return new ExponentNeuron();
				case NeuronType.GREATER_THAN:
					return new GreaterThanNeuron();
				case NeuronType.IF:
					return new IfNeuron();
				case NeuronType.INTEGRATE:
					return new IntegrateNeuron();
				case NeuronType.INTERPOLATE:
					return new InterpolateNeuron();
				case NeuronType.INTERPOLATE_ANGLE:
					return new InterpolateAngleNeuron();
				case NeuronType.INTERPOLATE_UNCLAMPED:
					return new InterpolateUnclampedNeuron();
				case NeuronType.IS_NAN:
					return new IsNanNeuron();
				case NeuronType.LOG:
					return new LogNeuron();
				case NeuronType.MAX:
					return new MaxNeuron();
				case NeuronType.MEMORY:
					return new MemoryNeuron();
				case NeuronType.MIN:
					return new MinNeuron();
				case NeuronType.NEGATE:
					return new NegateNeuron();
				case NeuronType.OSCIALLATE_SAW:
					return new OsciallatingSawNeuron();
				case NeuronType.OSCIALLATE_WAVE:
					return new OsciallatingWaveNeuron();
				case NeuronType.PRODUCT:
					return new ProductNeuron();
				case NeuronType.SIGMOID:
					return new SigmoidNeuron();
				case NeuronType.SIGN_OF:
					return new SignOfNeuron();
				case NeuronType.SIN:
					return new SinNeuron();
				case NeuronType.SMOOTH:
					return new SmoothNeuron();
				case NeuronType.SUM:
					return new SumNeuron();
				case NeuronType.SUM_THRESHOLD:
					return new SumThresholdNeuron();
				default:
					throw new Exception($"Could not recognize neuron type {type}.");
			}
		}
	}
}
