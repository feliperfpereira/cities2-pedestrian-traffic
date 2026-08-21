declare module "cs2/modding" {
  export type ModRegistrar = (moduleRegistry: {
    append: (slot: string, component: React.ComponentType) => void;
  }) => void;
}

declare module "cs2/api" {
  export interface ValueBinding<T> {}
  export function bindValue<T>(group: string, name: string, fallback: T): ValueBinding<T>;
  export function useValue<T>(binding: ValueBinding<T>): T;
}

interface Window {
  engine?: {
    call: (event: string, data?: string) => Promise<string>;
  };
}
