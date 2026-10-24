"""Writes the golden prompts of Gemma4Chat: the normalized conversations below are rendered by Google's own
chat_template.jinja (the file next to this script, from google/gemma-4-E2B-it) and kept as cases.json.
python3 make.py regenerates it."""
import json, jinja2

env = jinja2.Environment(trim_blocks=False, lstrip_blocks=False, extensions=["jinja2.ext.loopcontrols"])
def raise_exception(m): raise Exception(m)
env.globals["raise_exception"] = raise_exception
tpl = env.from_string(open("chat_template.jinja", encoding="utf-8").read())

TOOLS = [
  {"name": "ler_nota", "description": "Lê uma nota do vault.", "parameters": {"type": "object", "properties": {"caminho": {"type": "string", "description": "Caminho da nota"}}, "required": ["caminho"]}},
  {"name": "buscar", "description": "Busca notas por texto.", "parameters": {"type": "object", "properties": {
      "consulta": {"type": "string", "description": "O que procurar"},
      "limite": {"type": "integer", "description": "Máximo de resultados"},
      "pastas": {"type": "array", "description": "Onde procurar", "items": {"type": "string"}},
      "ordem": {"type": "string", "description": "Como ordenar", "enum": ["recentes", "nome"]},
      "exata": {"type": "boolean"}}, "required": ["consulta"]}},
  {"name": "editar", "description": "Muda trechos.", "parameters": {"type": "object", "properties": {
      "caminho": {"type": "string"},
      "trocas": {"type": "array", "description": "Trocas", "items": {"type": "object", "properties": {"de": {"type": "string"}, "para": {"type": "string"}}, "required": ["de", "para"]}},
      "opcoes": {"type": "object", "description": "Opções", "properties": {"Salvar": {"type": "boolean"}, "aviso": {"type": "string", "nullable": True}}}}, "required": ["caminho", "trocas"]}},
]
def text(t): return {"type": "text", "text": t}
def use(i, n, a): return {"type": "tool_use", "id": i, "name": n, "input": a}
def res(i, c): return {"type": "tool_result", "tool_use_id": i, "content": c}
CASES = {
  "user_only": {"messages": [{"role": "user", "content": [text("Oi!")]}]},
  "system_user": {"system": "  Você é o assistente do Urbe.  ", "messages": [{"role": "user", "content": [text("  Resuma minha nota.  ")]}]},
  "multi_turn": {"system": "Seja breve.", "messages": [
      {"role": "user", "content": [text("Quanto é 2+2?")]}, {"role": "assistant", "content": [text("4")]},
      {"role": "user", "content": [text("E 3+3?")]}]},
  "tools_declared": {"system": "Você ajuda.", "tools": TOOLS, "messages": [{"role": "user", "content": [text("Ache a nota do plano.")]}]},
  "tool_call_open": {"system": "Você ajuda.", "tools": TOOLS, "messages": [
      {"role": "user", "content": [text("Leia Plano.md")]},
      {"role": "assistant", "content": [use("a1", "ler_nota", {"caminho": "Plano.md"})]},
      {"role": "user", "content": [res("a1", "# Plano\n\nfazer **isso**")]}]},
  "tool_call_then_answer": {"system": "Você ajuda.", "tools": TOOLS, "messages": [
      {"role": "user", "content": [text("Leia Plano.md")]},
      {"role": "assistant", "content": [use("a1", "ler_nota", {"caminho": "Plano.md"})]},
      {"role": "user", "content": [res("a1", "# Plano")]},
      {"role": "assistant", "content": [text("A nota diz: Plano.")]},
      {"role": "user", "content": [text("Obrigado!")]}]},
  "two_calls": {"tools": TOOLS, "messages": [
      {"role": "user", "content": [text("Compare A e B")]},
      {"role": "assistant", "content": [use("c1", "ler_nota", {"caminho": "A.md"}), use("c2", "buscar", {"consulta": "B", "limite": 3, "exata": True, "pastas": ["x", "y"]})]},
      {"role": "user", "content": [res("c1", "texto A"), res("c2", "texto B")]}]},
}

def openai(case):
    msgs = []
    if case.get("system"): msgs.append({"role": "system", "content": case["system"]})
    for m in case["messages"]:
        blocks = m["content"]
        if m["role"] == "assistant":
            calls = [{"id": b["id"], "type": "function", "function": {"name": b["name"], "arguments": b["input"]}} for b in blocks if b["type"] == "tool_use"]
            msg = {"role": "assistant", "content": "".join(b["text"] for b in blocks if b["type"] == "text")}
            if calls: msg["tool_calls"] = calls
            msgs.append(msg)
        else:
            for b in blocks:
                if b["type"] == "tool_result": msgs.append({"role": "tool", "tool_call_id": b["tool_use_id"], "content": b["content"]})
            t = "".join(b["text"] for b in blocks if b["type"] == "text")
            if t or not any(b["type"] == "tool_result" for b in blocks): msgs.append({"role": m["role"], "content": t})
    return msgs

out = {}
for name, case in CASES.items():
    tools = [{"type": "function", "function": t} for t in case["tools"]] if case.get("tools") else None
    rendered = tpl.render(messages=openai(case), tools=tools, bos_token="<bos>", add_generation_prompt=True, enable_thinking=False)
    out[name] = {"request": {k: v for k, v in case.items()}, "expected": rendered}
json.dump(out, open("cases.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print({k: len(v["expected"]) for k, v in out.items()})
