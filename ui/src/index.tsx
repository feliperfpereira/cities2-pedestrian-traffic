import { ModRegistrar } from "cs2/modding";
import { TrafficApp } from "./traffic-app";

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("GameTopLeft", TrafficApp);
};

export default register;
