using Mono.Cecil.Cil;
using System;

namespace Mutator.Patching
{
    public struct BasicILCursor
    {
        private readonly MethodBody methodBody;

        public BasicILCursor(MethodBody methodBody)
        {
            this.methodBody = methodBody;
            Index = 0;
        }

        public int Index;

        public void Emit(Instruction instr)
        {
            if (methodBody == null) throw new Exception("Methodbody constructor parameter null");

            methodBody.Instructions.Insert(Index, instr);

            Index++;
        }

        public void Emit(params Instruction[] instrs)
        {
            if (methodBody == null) throw new Exception("Methodbody constructor parameter null");

            for (int i = instrs.Length - 1; i >= 0; i--) {
                methodBody.Instructions.Insert(Index, instrs[i]);
            }

            Index += instrs.Length;
        }
    }
}