using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection.Emit; 
using HarmonyLib;
using Qud.UI;                 
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.Core;
using ConsoleLib.Console;      
using XRL.Rules;
using Iterator;

namespace PictureMod
{
    // Parchea el método Description.HandleEvent(InventoryActionEvent)
    [HarmonyPatch(typeof(Description))]
    [HarmonyPatch(nameof(Description.HandleEvent))]
    [HarmonyPatch(new Type[] { typeof(InventoryActionEvent) })]
    public class Description_HandleEvent
    {
        // Here im just using the iterator that the wiki button mod provides.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions);

            matcher
                .Start()
                .MatchStartForward(new CodeMatch[]
                {
                    new(OpCodes.Ldsfld, AccessTools.Field(typeof(Options), nameof(Options.ModernUI))),
                    new(OpCodes.Brfalse_S),
                });

            if (matcher.IsInvalid)
            {
                Logger.buildLog.Error("PictureMod: no se encontró el bloque UI de inicio.");
                return instructions;
            }

            int start = matcher.Pos;
            var labels = matcher.Labels;

            matcher.MatchStartForward(new CodeMatch[]
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(IActOnItemEvent), nameof(IActOnItemEvent.Actor))),
                new(OpCodes.Ldstr, "LookedAt"),
                new(OpCodes.Ldstr, "Object"),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Callvirt, AccessTools.Method(typeof(IPart), "get_ParentObject", new Type[] {})),
                new(OpCodes.Call, AccessTools.Method(typeof(XRL.World.Event), nameof(XRL.World.Event.New), new Type[] { typeof(string), typeof(string), typeof(object) })),
                new(OpCodes.Callvirt, AccessTools.Method(typeof(GameObject), nameof(GameObject.FireEvent), new Type[] { typeof(XRL.World.Event) })),
                new(OpCodes.Pop),
            });

            if (matcher.IsInvalid)
            {
                Logger.buildLog.Error("PictureMod: no se encontró el final del bloque UI.");
                return instructions;
            }

            int end = matcher.Pos;

            var patch = new CodeInstruction[]
            {
                new(OpCodes.Nop) { labels = labels },
                new(OpCodes.Ldarg_0), // Description
                new(OpCodes.Ldloc_0), // TooltipInformation
                new(OpCodes.Ldloc_1), // StringBuilder mensaje
                new(OpCodes.Ldloc_2), // StringBuilder título
                new(OpCodes.Ldloc_3), // List<QudMenuItem>
                new(OpCodes.Call, AccessTools.Method(typeof(Description_HandleEvent), nameof(Detour)))
            };

            return matcher
                .Start()
                .Advance(start)
                .RemoveInstructions(end - start)
                .Insert(patch)
                .Instructions();
        }

        // Here we take the handle event and que set our own menu. I didn't wanted to change the name, Detour sounds really good :)
        private static void Detour(
            Description self,
            Look.TooltipInformation tooltip,
            StringBuilder message,
            StringBuilder title,
            List<Qud.UI.QudMenuItem> buttons)
        {
            // Change the wiki button for the Picture button
            buttons.Add(new Qud.UI.QudMenuItem
            {
                command = "Picture",
                hotkey = "P",
                text = "{{P|P}}icture"
            });

            // ---modern UI 
            if (Options.ModernUI)
            {
                var result = Popup.NewPopupMessageAsync(
                    message: message.ToString(),
                    buttons: buttons,
                    contextTitle: title.ToString(),
                    contextRender: tooltip.IconRenderable
                ).Result;

                if (result.command == "Picture")
                {
                    ShowPicturePopup(self, tooltip);
                }
            }
            // --- UI clásica ---
            else
            {
                title.Append("\n\n").Append(message);
                message.Clear();

                // Reutilizamos tus helpers Map + Intersperse
                var prompts = buttons.Map(x => x.text?.ToString() ?? "").Intersperse(", ");
                foreach (string prompt in prompts)
                    message.Append(prompt);

                var input = Popup.ShowBlockPrompt(
                    Message: title.ToString(),
                    Prompt: message.ToString(),
                    Icon: self.ParentObject.RenderForUI(),
                    Capitalize: false,
                    MuteBackground: true,
                    CenterIcon: false,
                    RightIcon: true,
                    LogMessage: false
                );

                if ((ConsoleKey)input == ConsoleKey.P)
                {
                    ShowPicturePopup(self, tooltip);
                }
            }
        }

        // === Función que muestra la imagen o popup ===
        private static void ShowPicturePopup(Description self, Look.TooltipInformation tooltip)
        {
            string objName = self.ParentObject.DisplayNameOnlyDirect ?? "Objeto";
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string fileName = objName + ".txt";
            string fullPath = Path.Combine(basePath, "img", fileName);

            string message = File.Exists(fullPath)
                ? File.ReadAllText(fullPath)
                : $"No se encontró información para \"{objName}\".";

            var renderable = tooltip.IconRenderable ?? self.ParentObject.RenderForUI();

            if (Options.ModernUI)
            {
                Popup.NewPopupMessageAsync(
                    message: message,
                    buttons: new List<Qud.UI.QudMenuItem>
                    {
                new Qud.UI.QudMenuItem { command = "Close", hotkey = "Escape", text = "Cerrar" }
                    },
                    contextTitle: "Imagen de objeto",
                    contextRender: renderable
                ).Wait();
            }
            else
            {
                Popup.Show(message);
            }
        }
    }
}