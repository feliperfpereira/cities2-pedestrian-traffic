import React, { useMemo } from "react";
import { bindValue, useValue } from "cs2/api";

type BackendState = {
  ready: boolean;
  toolActive: boolean;
  hasSelection: boolean;
  selectedIndex: number;
  pedestrian: boolean;
  freeRight: boolean;
  cityPedestrian: boolean;
  cityFreeRight: boolean;
};

const emptyState: BackendState = {
  ready: false,
  toolActive: false,
  hasSelection: false,
  selectedIndex: -1,
  pedestrian: false,
  freeRight: false,
  cityPedestrian: false,
  cityFreeRight: false,
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
  position: "fixed",
  right: "16px",
  top: "112px",
  width: "390px",
  maxWidth: "calc(100vw - 32px)",
  maxHeight: "calc(100vh - 136px)",
  boxSizing: "border-box",
  overflowY: "auto",
  overflowX: "hidden",
  padding: "14px",
  borderRadius: "10px",
  background: "rgba(30, 34, 40, 0.96)",
  color: "white",
  boxShadow: "0 8px 28px rgba(0,0,0,.38)",
  zIndex: 9999,
  fontFamily: "Arial, sans-serif",
};

const floatingStyle: React.CSSProperties = {
  position: "fixed",
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
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
};

const lightIconStyle: React.CSSProperties = {
  display: "inline-flex",
  flexDirection: "column",
  alignItems: "center",
  justifyContent: "center",
  gap: "2px",
  width: "15px",
  height: "25px",
  padding: "3px 2px",
  borderRadius: "5px",
  background: "rgba(0, 0, 0, .42)",
  border: "1px solid rgba(255,255,255,.25)",
};

const lightDotStyle: React.CSSProperties = {
  width: "5px",
  height: "5px",
  borderRadius: "50%",
  display: "block",
};

const rowStyle: React.CSSProperties = {
  display: "flex",
  alignItems: "flex-start",
  justifyContent: "space-between",
  gap: "12px",
  minWidth: 0,
  padding: "11px 0",
  borderTop: "1px solid rgba(255,255,255,.10)",
};

const featureTextStyle: React.CSSProperties = {
  flex: "1 1 auto",
  minWidth: 0,
  overflowWrap: "anywhere",
};

function Toggle({
  enabled,
  onClick,
  disabled = false,
}: {
  enabled: boolean;
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      style={{
        minWidth: "84px",
        flex: "0 0 auto",
        whiteSpace: "nowrap",
        padding: "7px 10px",
        borderRadius: "7px",
        border: "1px solid rgba(255,255,255,.18)",
        background: enabled ? "rgba(63, 180, 108, .9)" : "rgba(90, 96, 105, .9)",
        color: "white",
        cursor: disabled ? "not-allowed" : "pointer",
        fontWeight: 700,
        opacity: disabled ? .55 : 1,
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
        aria-label="Semáforos: pedestres e direita livre"
        style={floatingStyle}
        onClick={() => action("toggleTool")}
      >
        <span aria-hidden="true" style={lightIconStyle}>
          <span style={{ ...lightDotStyle, background: "#ef5350" }} />
          <span style={{ ...lightDotStyle, background: "#ffd54f" }} />
          <span style={{ ...lightDotStyle, background: "#66bb6a" }} />
        </span>
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
                <div style={featureTextStyle}>
                  <div style={{ fontWeight: 700 }}>Fase só para pedestres</div>
                  <div style={{ opacity: .68, fontSize: "11px", marginTop: "3px" }}>
                    Todos os carros param; apenas pedestres passam.
                  </div>
                </div>
                <Toggle
                  enabled={state.pedestrian}
                  disabled={state.cityPedestrian}
                  onClick={() => action("togglePedestrian")}
                />
              </div>

              <div style={rowStyle}>
                <div style={featureTextStyle}>
                  <div style={{ fontWeight: 700 }}>Direita livre</div>
                  <div style={{ opacity: .68, fontSize: "11px", marginTop: "3px" }}>
                    Vira à direita em cedência; fica vermelho para pedestres.
                  </div>
                </div>
                <Toggle
                  enabled={state.freeRight}
                  disabled={state.cityFreeRight}
                  onClick={() => action("toggleRight")}
                />
              </div>

              <div style={{ marginTop: "9px", opacity: .62, fontSize: "10px", lineHeight: 1.4 }}>
                A direita livre ativa automaticamente a fase exclusiva de pedestres por segurança.
              </div>
            </>
          )}

          <div style={{ marginTop: "14px", paddingTop: "12px", borderTop: "1px solid rgba(255,255,255,.16)" }}>
            <div style={{ fontSize: "14px", fontWeight: 800 }}>Cidade inteira</div>
            <div style={{ marginTop: "3px", opacity: .68, fontSize: "11px" }}>
              Aplica a todos os cruzamentos com semáforo e preserva as configurações individuais.
            </div>

            <div style={{ ...rowStyle, marginTop: "8px" }}>
              <div style={featureTextStyle}>
                <div style={{ fontWeight: 700 }}>Fase só para pedestres</div>
                <div style={{ opacity: .68, fontSize: "11px", marginTop: "3px" }}>
                  Inclui cruzamentos novos quando forem criados.
                </div>
              </div>
              <Toggle enabled={state.cityPedestrian} onClick={() => action("toggleCityPedestrian")} />
            </div>

            <div style={rowStyle}>
              <div style={featureTextStyle}>
                <div style={{ fontWeight: 700 }}>Direita livre</div>
                <div style={{ opacity: .68, fontSize: "11px", marginTop: "3px" }}>
                  Também ativa a fase exclusiva de pedestres na cidade inteira.
                </div>
              </div>
              <Toggle enabled={state.cityFreeRight} onClick={() => action("toggleCityRight")} />
            </div>
          </div>
        </div>
      )}
    </>
  );
};
