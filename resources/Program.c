#if __cplusplus
extern "C" {
#endif

#ifdef _MSC_VER
#define inline __inline
#endif

typedef unsigned int _helix_bool;
typedef unsigned int _helix_void;
typedef unsigned int _helix_int;

typedef struct _Region {
	unsigned int depth;
} _Region;

extern _Region* _region_new();
extern void* _region_malloc(_Region* region, int size);
extern void _region_delete(_Region* region);

typedef union IntOption$union IntOption$union;
typedef struct IntOption IntOption;

IntOption test(_Region* _return_region, IntOption x);


union IntOption$union {
    int none;
    int some;
    int other;
};

struct IntOption {
    int tag;
    IntOption$union data;
};

IntOption test(_Region* _return_region, IntOption x) {
    /* Line 9: If statement */
    if ((((x.tag) == 2U) | ((x.tag) == 0U))) {     } 
    else {
        /* Line 9: Union downcast flowtyping */
        int* x_1 = (&((x.data).some));

        /* Line 14: Assignment statement */
        (*x_1) = ((*x_1) + 45U);
    }

    return x;
}

#if __cplusplus
}
#endif
