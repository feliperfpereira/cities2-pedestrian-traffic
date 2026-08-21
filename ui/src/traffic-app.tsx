import React, { useMemo } from "react";
import { bindValue, useValue } from "cs2/api";

type BackendState = {
  ready: boolean;
  toolActive: boolean;
  hasSelection: boolean;
  selectedIndex: number;
  pedestrian: boolean;
  freeRight: boolean;
};

const emptyState: BackendState = {
  ready: false,
  toolActive: false,
  hasSelection: false,
  selectedIndex: -1,
  pedestrian: false,
  freeRight: false,
};

const state$ = bindValue<string>(
  "Cities2PedestrianTraffic",
  "State",
  JSON.stringify(emptyState),
);

async function action(name: string) {
  if (window.engine) {
    await window.engine.call("Cities2PedestrianTraffic.Action", name);
  }
}

const panelStyle: React.CSSProperties = {
  position: "absolute",
  right: "24px",
  top: "112px",
  width: "330px",
  padding: "14px",
  borderRadius: "10px",
  background: "rgba(30, 34, 40, 0.96)",
  color: "white",
  boxShadow: "0 8px 28px rgba(0,0,0,.38)",
  zIndex: 9999,
  fontFamily: "Arial, sans-serif",
};

const floatingStyle: React.CSSProperties = {
  position: "absolute",
  right: "24px",
  top: "62px",
  width: "42px",
  height: "42px",
  borderRadius: "9px",
  border: "1px solid rgba(255,255,255,.2)",
  background: "rgba(30, 34, 40, .94)",
  color: "white",
  cursor: "pointer",
  zIndex: 9999,
  fontSize: "21px",
};

const rowStyle: React.CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  gap: "12px",
  padding: "11px 0",
  borderTop: "1px solid rgba(255,255,255,.10)",
};

function Toggle({ enabled, onClick }: { enabled: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      style={{
        minWidth: "84px",
        padding: "7px 10px",
        borderRadius: "7px",
        border: "1px solid rgba(255,255,255,.18)",
        background: enabled ? "rgba(63, 180, 108, .9)" : "rgba(90, 96, 105, .9)",
        color: "white",
        cursor: "pointer",
        fontWeight: 700,
      }}
    >
      {enabled ? "Ativado" : "Desativado"}
    </button>
  );
}

export const TrafficApp = () => {
  const rawState = useValue(state$);
  const state = useMemo<BackendState>(() => {
    try {
      return { ...emptyState, ...JSON.parse(rawState) };
    } catch {
      return emptyState;
    }
  }, [rawState]);

  if (!state.ready) {
    return null;
  }

  const showPanel = state.toolActive || state.hasSelection;

  return (
    <>
      <button
        title="Semáforos: pedestres e direita livre"
        style={floatingStyle}
        onClick={() => action("toggleTool")}
      >
        🚦
      </button>

      {showPanel && (
        <div style={panelStyle}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "10px" }}>
            <div>
              <div style={{ fontSize: "16px", fontWeight: 800 }}>Semáforo</div>
              <div style={{ marginTop: "3px", opacity: .72, fontSize: "12px" }}>
                {state.hasSelection ? `Cruzamento #${state.selectedIndex}` : "Clique em um semáforo"}
              </div>
            </div>
            <button
              onClick={() => action("close")}
              style={{ border: 0, background: "transparent", color: "white", cursor: "pointer", fontSize: "18px" }}
            >
              ×
            </button>
          </div>

          {state.hasSelection && (
            <>
              <div style={{ ...rowStyle, marginTop: "10px" }}>
                <div>
                  <div style={{ fontWeight: 700 }}>Fase só para pedestres</div>
                  <div style={{ opacity: .68, fontSize: "11px", marginTop: "3px" }}>
                    Todos os carros param; apenas pedestres passam.
                  </div>
                </div>
                <Toggle enabled={state.pedestrian} onClick={() => action("togglePedestrian")} />
              </div>

              <div style={rowStyle}>
                <div>
                  <div style={{ fontWeight: 700 }}>Direita livre</div>
                  <div style={{ opacity: .68, fontSize: "11px", marginTop: "3px" }}>
                    Vira à direita em cedência; fica vermelho para pedestres.
                  </div>
                </div>
                <Toggle enabled={state.freeRight} onClick={() => action("toggleRight")} />
              </div>

              <div style={{ marginTop: "9px", opacity: .62, fontSize: "10px", lineHeight: 1.4 }}>
                A direita livre ativa automaticamente a fase exclusiva de pedestres por segurança.
              </div>
            </>
          )}
        </div>
      )}
    </>
  );
};
