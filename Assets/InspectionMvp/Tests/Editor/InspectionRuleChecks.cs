using System;
using UnityEditor;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp.Tests
{
    /// <summary>Dependency-free assertions callable from Pipeline run_script or the Editor menu.</summary>
    public static class InspectionRuleChecks
    {
        [MenuItem("Tools/She Is Not Human/Inspection MVP/Check Customer Code Rule")]
        public static void RunFromMenu() => Debug.Log("Inspection rule checks passed: " + Run());

        public static int Run()
        {
            int checkedCount = 0;
            var npc = ScriptableObject.CreateInstance<InspectionNpcData>();
            try
            {
                Expect(null, InspectionDecision.NonPass, "missing NPC", ref checkedCount);
                npc.identity = null;
                Expect(npc, InspectionDecision.NonPass, "missing identity", ref checkedCount);
                npc.identity = new IdentityDocument();
                npc.order = null;
                Expect(npc, InspectionDecision.NonPass, "missing order", ref checkedCount);
                npc.order = new OrderDocument();
                CheckCodes(npc, null, null, InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "", "", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-101", null, InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-101", "C-101", InspectionDecision.Pass, ref checkedCount);
                CheckCodes(npc, "C-202", "C-209", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-303", "C-303", InspectionDecision.Pass, ref checkedCount);
                CheckCodes(npc, "C-101", "c-101", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-101", "C-101 ", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, " C-101", "C-101", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, "C-101", "C-101\n", InspectionDecision.NonPass, ref checkedCount);
                CheckCodes(npc, " C-101 ", " C-101 ", InspectionDecision.Pass, ref checkedCount);
                npc.identity.displayName = "different identity name";
                npc.displayName = "different NPC name";
                npc.order.quantity = -10;
                npc.order.orderNumber = null;
                npc.order.productName = null;
                CheckCodes(npc, "C-101", "C-101", InspectionDecision.Pass, ref checkedCount);
                return checkedCount;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(npc);
            }
        }

        private static void CheckCodes(InspectionNpcData npc, string identity, string order,
            InspectionDecision expected, ref int checkedCount)
        {
            npc.identity.customerCode = identity;
            npc.order.customerCode = order;
            Expect(npc, expected, "codes [" + identity + "] / [" + order + "]", ref checkedCount);
        }

        private static void Expect(InspectionNpcData npc, InspectionDecision expected, string scenario, ref int checkedCount)
        {
            var actual = InspectionRule.ExpectedDecision(npc);
            if (actual != expected)
                throw new InvalidOperationException("Inspection rule failed: " + scenario + "; expected " + expected + ", got " + actual);
            checkedCount++;
        }
    }
}
