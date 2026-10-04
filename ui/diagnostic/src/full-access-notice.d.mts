export declare const fullAccessNoticeStorageKey: string;
export declare function isFullAccessNoticeHidden(storage?: Pick<Storage, "getItem">): boolean;
export declare function hideFullAccessNotice(storage?: Pick<Storage, "setItem">): boolean;
