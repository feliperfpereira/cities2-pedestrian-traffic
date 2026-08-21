import { ModRegistrar } from "cs2/modding";
import { TrafficApp } from "./traffic-app";

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("Menu", TrafficApp);
};

export default register;
